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
        var isDelegate = type.BaseType?.FullName == "System.MulticastDelegate";

        var ctors = isDelegate
            ? Array.Empty<ConstructorInfo>()
            : type.GetConstructors();

        var methods = isDelegate
            ? Array.Empty<MethodInfo>()
            : type
                .GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                .Where(m => !m.IsSpecialName)
                .ToArray();

        var properties = type
                .GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                .Where(m => !m.IsSpecialName)
                .ToArray();

        var xrefs = methods
            .Select(m => KeyValuePair.Create(
                Xref.Create(m),
                new Reference.Resolved(m.Name, new Uri(url + "#" + m.UrlFriendlyName(), UriKind.Relative))))
            .Concat(properties.Select(p => KeyValuePair.Create(
                Xref.Create(p),
                new Reference.Resolved(p.Name, new Uri(url + "#" + p.UrlFriendlyName(), UriKind.Relative))))
            )
            .Prepend(KeyValuePair.Create(Xref.Create(type), new Reference.Resolved(title, url)))
            .ToImmutableDictionary();

        var typeDoc = doc.GetDoc(type);
        var summarySection = typeDoc
            .Element("summary")
            ?.Nodes()
            .Select(Markup.FromXml)
            .Prepend(new Markup.SectionHeader("Summary", 2, "summary"))
            .Concat(typeDoc.Elements("example").Select(Markup.FromXml).Prepend(new Markup.SectionHeader("Examples", 2, "examples")))
            ?? Enumerable.Empty<Markup>();

        var ctorsSection = ctors.Any()
            ? ctors
                .SelectMany(m => Method(m, doc))
                .Prepend(new Markup.SectionHeader("Constructors", 2, "constructors"))
            : Enumerable.Empty<Markup>();

        var methodsSection = methods.Any()
            ? methods
                .SelectMany(m => Method(m, doc))
                .Prepend(new Markup.SectionHeader("Methods", 2, "methods"))
            : Enumerable.Empty<Markup>();

        var propertiesSection = properties.Any()
            ? properties
                .SelectMany(p => Property(p, doc))
                .Prepend(new Markup.SectionHeader("Properties", 2, "properties"))
            : Enumerable.Empty<Markup>();

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
        );
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

    private static IEnumerable<Markup> Method(MethodBase meth, XmlDocFile docFile)
    {
        var ret = (meth as MethodInfo)?.ReturnType.FriendlyName();
        var declaration = string.IsNullOrEmpty(ret)
            ? meth.FriendlyName()
            : ret + " " + meth.FriendlyName();

        return docFile.GetDoc(meth)
            .Element("summary")
            ?.Nodes()
            .Select(Markup.FromXml)
            .Append(new Markup.Seq(new Markup.SectionHeader("Declaration", 4, null), new Markup.CodeBlock(declaration)))
            .Prepend(new Markup.SectionHeader(meth.FriendlyName(), 3, meth.UrlFriendlyName()))
            ?? Enumerable.Empty<Markup>();
    }

    private static IEnumerable<Markup> Property(PropertyInfo prop, XmlDocFile docFile)
    {
        return docFile.GetDoc(prop)
            .Element("summary")
            ?.Nodes()
            .Select(Markup.FromXml)
            .Prepend(new Markup.SectionHeader(prop.FriendlyName(), 3, prop.UrlFriendlyName()))
            ?? Enumerable.Empty<Markup>();
    }
}
