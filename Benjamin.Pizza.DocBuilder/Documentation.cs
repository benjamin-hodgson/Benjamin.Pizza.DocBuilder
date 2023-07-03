using System.Collections.Immutable;
using System.Reflection;

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
        var pages = ImmutableArray.CreateBuilder<DocumentationPage>();
        var xrefs = ImmutableDictionary.CreateBuilder<Xref, Reference.Resolved>();
        foreach (var (asm, docFile) in assemblies)
        {
            foreach (var ty in asm.GetExportedTypes())
            {
                var page = DocumentationPage.Create(ty, docFile);
                pages.Add(page);
                xrefs.AddRange(page.ContainedReferences);
            }

            foreach (var ns in asm.GetExportedTypes().GroupBy(t => t.Namespace).Where(g => g.Key != null))
            {
                pages.Add(NamespaceDocumentationPage.Create(ns!, docFile));
            }
        }

        return new Documentation(pages.ToImmutable(), xrefs.ToImmutable());
    }
}

internal record DocumentationPage(
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

        var typeDoc = doc.GetDoc(type);
        var summarySection = typeDoc
            .Element("summary")
            ?.Nodes()
            .Select(Markup.FromXml)
            .Prepend(new Markup.SectionHeader("Summary", 2, "summary"))
            .Concat(typeDoc.Elements("example").Select(Markup.FromXml).PrependIfNotEmpty(new Markup.SectionHeader("Examples", 2, "examples")))
            ?? Enumerable.Empty<Markup>();

        var (ctorRefs, ctorsSection) = Load(type, url, doc, new ConstructorDocumentationLoader());
        var (methodRefs, methodsSection) = Load(type, url, doc, new MethodDocumentationLoader());
        var (propertyRefs, propertiesSection) = Load(type, url, doc, new PropertyDocumentationLoader());
        var (fieldRefs, fieldsSection) = Load(type, url, doc, new FieldDocumentationLoader());

        return new DocumentationPage(
            url,
            title,
            new Markup.Seq(
                summarySection
                    .Concat(ctorsSection)
                    .Concat(methodsSection)
                    .Concat(propertiesSection)
                    .Concat(fieldsSection)
                    .ToImmutableArray()
            ),
            ImmutableDictionary<Xref, Reference.Resolved>
                .Empty
                .Add(Xref.Create(type), new Reference.Resolved(title, url))
                .AddRange(ctorRefs)
                .AddRange(methodRefs)
                .AddRange(propertyRefs)
                .AddRange(fieldRefs)
        );
    }

    protected static (ImmutableDictionary<Xref, Reference.Resolved>, IEnumerable<Markup>) Load<TContext, T>(
        TContext type,
        Uri baseUrl,
        XmlDocFile doc,
        IDocumentationSectionLoader<TContext, T> loader)
    {
        var refs = ImmutableDictionary.CreateBuilder<Xref, Reference.Resolved>();
        var docs = ImmutableArray.CreateBuilder<Markup>();

        foreach (var item in loader.GetItems(type))
        {
            var xref = loader.GetXref(item);
            var fragment = loader.Load(item, doc.GetDoc(xref));
            refs.Add(xref, new Reference.Resolved(fragment.Name, new Uri(baseUrl + fragment.UrlFragment, UriKind.Relative)));
            docs.AddRange(fragment.Markup);
        }

        return (refs.ToImmutable(), docs.PrependIfNotEmpty(loader.SectionHeader));
    }
}

internal sealed record NamespaceDocumentationPage(
    Uri Url,
    string Title,
    Markup Body,
    ImmutableDictionary<Xref, Reference.Resolved> ContainedReferences
) : DocumentationPage(Url, Title, Body, ContainedReferences)
{
    public static DocumentationPage Create(IGrouping<string, Type> g, XmlDocFile doc)
    {
        var url = new Uri(UrlExtensions.MakeUrlFriendly(g.Key) + ".html", UriKind.Relative);

        // ignore the ContainedReferences as the namespace
        // page is not the canonical source for the members
        // of the namespace
        var (_, classes) = Load(g, url, doc, new ClassDocumentationLoader());
        var (_, interfaces) = Load(g, url, doc, new InterfaceDocumentationLoader());
        var (_, delegates) = Load(g, url, doc, new DelegateDocumentationLoader());
        var (_, enums) = Load(g, url, doc, new EnumDocumentationLoader());
        var (_, structs) = Load(g, url, doc, new StructDocumentationLoader());

        return new DocumentationPage(
            url,
            g.Key,
            new Markup.Seq(
                classes
                    .Concat(interfaces)
                    .Concat(delegates)
                    .Concat(enums)
                    .Concat(structs)
                    .ToImmutableArray()
            ),
            ImmutableDictionary<Xref, Reference.Resolved>.Empty);
    }
}
