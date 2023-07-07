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

        var typeDoc = doc.GetDoc(type);
        var summarySection = typeDoc
            .Element("summary")
            ?.Nodes()
            .Select(Markup.FromXml)
            .Prepend(new Markup.SectionHeader("Summary", 2, "summary"))
            ?? Enumerable.Empty<Markup>();

        var declarationSection = new Markup[]
        {
            new Markup.SectionHeader("Declaration", 2, "declaration"),
            Markup.CreateCodeBlock(type.GetDeclaration())
        };

        var examplesSection = typeDoc
            .Elements("example")
            .Select(Markup.FromXml)
            .PrependIfNotEmpty(new Markup.SectionHeader("Examples", 2, "examples"));

        var remarksSection = typeDoc
            .Element("remarks")
            ?.Nodes()
            .Select(Markup.FromXml)
            .Prepend(new Markup.SectionHeader("Remarks", 2, "remarks"))
            ?? Enumerable.Empty<Markup>();

        var seeAlsoItems = typeDoc
            .Elements("seealso")
            .Select(e => new Markup.Link(new Reference.Unresolved(Xref.FromCref(e))))
            .Cast<Markup>()
            .ToImmutableArray();
        var seeAlsoSection = seeAlsoItems.Length == 0
            ? Enumerable.Empty<Markup>()
            : ImmutableArray.Create<Markup>(
                new Markup.SectionHeader("See Also", 2, "seealso"),
                new Markup.List(seeAlsoItems));

        var (ctorRefs, ctorsSection) = Load(type, url, doc, new ConstructorDocumentationSection());
        var (methodRefs, methodsSection) = Load(type, url, doc, new MethodDocumentationSection());
        var (propertyRefs, propertiesSection) = Load(type, url, doc, new PropertyDocumentationSection());
        var (fieldRefs, fieldsSection) = Load(type, url, doc, new FieldDocumentationSection());

        return new DocumentationPage(
            url,
            type.FriendlyName(),
            new Markup.Seq(
                summarySection
                    .Concat(declarationSection)
                    .Concat(remarksSection)
                    .Concat(examplesSection)
                    .Concat(ctorsSection)
                    .Concat(methodsSection)
                    .Concat(propertiesSection)
                    .Concat(fieldsSection)
                    .Concat(seeAlsoSection)
                    .ToImmutableArray()
            ),
            ImmutableDictionary<Xref, Reference.Resolved>
                .Empty
                .Add(Xref.Create(type), new Reference.Resolved(type.FriendlyName(), url))
                .AddRange(ctorRefs)
                .AddRange(methodRefs)
                .AddRange(propertyRefs)
                .AddRange(fieldRefs)
        );
    }

    protected static (ImmutableDictionary<Xref, Reference.Resolved>, IEnumerable<Markup>) Load<T>(
        Type type,
        Uri baseUrl,
        XmlDocFile doc,
        ITypeDocumentationSection<T> section)
    {
        var refs = ImmutableDictionary.CreateBuilder<Xref, Reference.Resolved>();
        var docs = ImmutableArray.CreateBuilder<Markup>();

        foreach (var item in section.GetItems(type))
        {
            var xref = section.GetXref(item);
            var fragment = section.Load(item, doc.GetDoc(xref));
            refs.Add(xref, new Reference.Resolved(fragment.Name, new Uri(baseUrl + fragment.UrlFragment, UriKind.Relative)));
            docs.AddRange(fragment.Markup);
        }

        return (refs.ToImmutable(), docs.PrependIfNotEmpty(section.SectionHeader));
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

        var classes = Load(g, doc, new ClassDocumentationSection());
        var interfaces = Load(g, doc, new InterfaceDocumentationSection());
        var delegates = Load(g, doc, new DelegateDocumentationSection());
        var enums = Load(g, doc, new EnumDocumentationSection());
        var structs = Load(g, doc, new StructDocumentationSection());

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

    private static IEnumerable<Markup> Load(IEnumerable<Type> namespaceMembers, XmlDocFile doc, INamespaceDocumentationSection section)
    {
        foreach (var type in namespaceMembers.Where(section.ShouldIncludeType))
        {
            var xref = Xref.Create(type);
            var markup = section.GetMarkup(type, doc.GetDoc(xref));
            yield return new Markup.Seq(markup.ToImmutableArray());
        }
    }
}
