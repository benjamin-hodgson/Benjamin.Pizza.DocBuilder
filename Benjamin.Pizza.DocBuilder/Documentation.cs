using System.Collections.Immutable;
using System.Reflection;
using System.Xml.Linq;

using Sawmill;

namespace Benjamin.Pizza.DocBuilder;

internal sealed record Documentation(ImmutableArray<DocumentationPage> Pages, ImmutableDictionary<Xref, Reference.Resolved> Xrefs)
{
    public async Task<Documentation> UpdateMarkupAsync(Func<Markup, ValueTask<Markup>> func)
    {
        async Task<DocumentationPage> UpdatePage(DocumentationPage p)
            => p with
            {
                Body = await p.Body.Rewrite(func).ConfigureAwait(false)
            };

        var newPages = await Task.WhenAll(Pages.Select(UpdatePage)).ConfigureAwait(false);

        return this with { Pages = newPages.ToImmutableArray() };
    }

    public static Documentation FromAssemblies(IEnumerable<(Assembly, XmlDocFile)> assemblies)
    {
        var typePages = (
            from tup in assemblies
            from ty in tup.Item1.GetExportedTypes()
            select DocumentationPage.Create(ty, tup.Item2)
        ).ToList();

        var nsPages =
            from tup in assemblies
            from ty in tup.Item1.GetExportedTypes()
            group (ty, tup.Item2.GetDoc(ty)) by ty.Namespace into g
            select DocumentationPage.Create(g);

        var xrefs = typePages.SelectMany(t => t.ContainedReferences).ToImmutableDictionary();

        return new Documentation(typePages.Concat(nsPages).ToImmutableArray(), xrefs);
    }
}

internal sealed record DocumentationPage(
    Uri Url,
    string Title,
    Markup Body,
    ImmutableDictionary<Xref, Reference.Resolved> ContainedReferences
)
{
    public static DocumentationPage Create(Type type, XmlDocFile doc)
    {
        var url = new Uri(type.UrlFriendlyName() + ".html", UriKind.Relative);
        var title = type.FriendlyName();

        var xrefs = ImmutableDictionary<Xref, Reference.Resolved>
            .Empty
            .Add(Xref.Create(type), new Reference.Resolved(title, url));

        var typeDoc = doc.GetDoc(type);
        var summarySection = typeDoc
            .Element("summary")
            ?.Nodes()
            .Select(Markup.FromXml)
            .Prepend(new Markup.SectionHeader("Summary", 2, "summary"))
            .Concat(typeDoc.Elements("example").Select(Markup.FromXml).Prepend(new Markup.SectionHeader("Examples", 2, "examples")))
            ?? Enumerable.Empty<Markup>();

        var (ctorRefs, ctorsSection) = Load(type, url, doc, ConstructorDocumentationLoader.Instance);
        var (methodRefs, methodsSection) = Load(type, url, doc, MethodDocumentationLoader.Instance);
        var (propertyRefs, propertiesSection) = Load(type, url, doc, PropertyDocumentationLoader.Instance);

        return new DocumentationPage(
            url,
            title,
            new Markup.Seq(
                summarySection
                    .Concat(ctorsSection)
                    .Concat(methodsSection)
                    .Concat(propertiesSection)
                    .ToImmutableArray()
            ),
            xrefs
                .AddRange(ctorRefs)
                .AddRange(methodRefs)
                .AddRange(propertyRefs)
        );
    }

    private static (ImmutableDictionary<Xref, Reference.Resolved>, IEnumerable<Markup>) Load<T>(
        Type type,
        Uri baseUrl,
        XmlDocFile doc,
        IDocumentationSectionLoader<T> loader)
    {
        var refs = ImmutableDictionary<Xref, Reference.Resolved>.Empty;
        var docs = ImmutableArray.CreateBuilder<Markup>();

        foreach (var item in loader.GetItems(type))
        {
            var xref = loader.GetXref(item);
            var fragment = loader.Load(item, doc.GetDoc(xref));
            refs.Add(xref, new Reference.Resolved(fragment.Name, new Uri(baseUrl + fragment.UrlFragment, UriKind.Relative)));
            docs.AddRange(fragment.Markup);
        }

        return (refs, docs.PrependIfNotEmpty(loader.SectionHeader));
    }

    public static DocumentationPage Create(IGrouping<string, (Type, XElement)> g)
    {
        var url = new Uri(UrlExtensions.MakeUrlFriendly(g.Key) + ".html", UriKind.Relative);

        var classes = g
            .Where(tup => tup.Item1.IsClass)
            .SelectMany(tup =>
                (tup.Item2.Element("summary")?.Nodes() ?? Enumerable.Empty<XNode>())
                    .Select(Markup.FromXml)
                    .Prepend(new Markup.SectionHeader(new Markup.Link(new Reference.Unresolved(Xref.Create(tup.Item1))), 3, null)))
            .PrependIfNotEmpty(new Markup.SectionHeader("Classes", 1, "classes"));

        var interfaces = g
            .Where(tup => tup.Item1.IsInterface)
            .SelectMany(tup =>
                (tup.Item2.Element("summary")?.Nodes() ?? Enumerable.Empty<XNode>())
                    .Select(Markup.FromXml)
                    .Prepend(new Markup.SectionHeader(new Markup.Link(new Reference.Unresolved(Xref.Create(tup.Item1))), 3, null)))
            .PrependIfNotEmpty(new Markup.SectionHeader("Interfaces", 1, "interfaces"));

        var enums = g
            .Where(tup => tup.Item1.IsEnum)
            .SelectMany(tup =>
                (tup.Item2.Element("summary")?.Nodes() ?? Enumerable.Empty<XNode>())
                    .Select(Markup.FromXml)
                    .Prepend(new Markup.SectionHeader(new Markup.Link(new Reference.Unresolved(Xref.Create(tup.Item1))), 3, null)))
            .PrependIfNotEmpty(new Markup.SectionHeader("Enums", 1, "enums"));

        var structs = g
            .Where(tup => tup.Item1.IsValueType && !tup.Item1.IsEnum)
            .SelectMany(tup =>
                (tup.Item2.Element("summary")?.Nodes() ?? Enumerable.Empty<XNode>())
                    .Select(Markup.FromXml)
                    .Prepend(new Markup.SectionHeader(new Markup.Link(new Reference.Unresolved(Xref.Create(tup.Item1))), 3, null)))
            .PrependIfNotEmpty(new Markup.SectionHeader("Structs", 1, "structs"));

        return new DocumentationPage(
            url,
            g.Key,
            new Markup.Seq(classes.Concat(interfaces).Concat(enums).Concat(structs).ToImmutableArray()),
            ImmutableDictionary<Xref, Reference.Resolved>.Empty);
    }
}
