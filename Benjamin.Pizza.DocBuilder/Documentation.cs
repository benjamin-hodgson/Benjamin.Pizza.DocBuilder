using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;
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
        var pages = assemblies
            .SelectMany(asm => asm.Item1
                .GetExportedTypes()
                .Select(ty => DocumentationPage.Create(ty, asm.Item2))
            )
            .ToImmutableArray();

        var xrefs = pages.SelectMany(p => p.ContainedReferences).ToImmutableDictionary();

        return new Documentation(pages, xrefs);
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
        var url = new Uri(UrlExtensions.UrlFriendlyName(type.FullName!) + ".html", UriKind.Relative);
        var title = type.FriendlyName();

        var methods = type.BaseType?.FullName == "System.MulticastDelegate"
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
                new Reference.Resolved(m.Name, new Uri(url + "#" + UrlExtensions.UrlFriendlyName(m.Name), UriKind.Relative))))
            .Concat(properties.Select(p => KeyValuePair.Create(
                Xref.Create(p),
                new Reference.Resolved(p.Name, new Uri(url + "#" + UrlExtensions.UrlFriendlyName(p.Name), UriKind.Relative))))
            )
            .Prepend(KeyValuePair.Create(Xref.Create(type), new Reference.Resolved(title, url)))
            .ToImmutableDictionary();

        var summarySection = doc.GetDoc(type)
                .Element("summary")
                ?.Nodes()
                .Select(Markup.FromXml)
                .Prepend(new Markup.SectionHeader("Summary", 2, "summary"))
                ?? Enumerable.Empty<Markup>();

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
            new Markup.Seq(summarySection.Concat(methodsSection).ToImmutableArray()),
            xrefs
        );
    }

    private static IEnumerable<Markup> Method(MethodBase meth, XmlDocFile doc)
    {
        return doc.GetDoc(meth)
            .Element("summary")
            ?.Nodes()
            .Select(Markup.FromXml)
            ?? Enumerable.Empty<Markup>();
    }

    private static IEnumerable<Markup> Property(PropertyInfo prop, XmlDocFile doc)
    {
        return doc.GetDoc(prop)
            .Element("summary")
            ?.Nodes()
            .Select(Markup.FromXml)
            ?? Enumerable.Empty<Markup>();
    }
}

internal abstract partial record Markup : IRewritable<Markup>
{
    public sealed record Seq(ImmutableArray<Markup> Values) : Markup;
    public sealed record SectionHeader(string Title, int Level, string Id) : Markup;
    public sealed record Paragraph(Markup Content) : Markup;
    public sealed record Text(string Value) : Markup;
    public sealed record Link(Reference Reference) : Markup;
    public sealed record InlineCode(string Code) : Markup;

    public static Markup FromXml(XNode node)
        => node switch
        {
            XText t => new Text(Whitespaces().Replace(t.Value, " ")),
            XElement e => e.Name.LocalName switch
            {
                "see" => new Link(new Reference.Unresolved(Xref.FromCref(e))),
                "para" => new Paragraph(new Seq(e.Elements().Select(FromXml).ToImmutableArray())),
                "c" => new InlineCode(((XText)e.FirstNode!).Value),
                "paramref" or "typeparamref" => new InlineCode(e.Attribute("name")!.Value),
                var n => new Text("???" + n)
            },
            var n => new Text($"???{n.NodeType}")
        };

    public int CountChildren()
        => this switch
        {
            Seq(var children) => children.Length,
            Paragraph => 1,
            _ => 0
        };

    public void GetChildren(Span<Markup> childrenReceiver)
    {
        switch (this)
        {
            case Seq(var children):
                children.CopyTo(childrenReceiver);
                break;
            case Paragraph(var content):
                childrenReceiver[0] = content;
                break;
        }
    }

    public Markup SetChildren(ReadOnlySpan<Markup> newChildren)
        => this switch
        {
            Seq s => s with { Values = newChildren.ToImmutableArray() },
            Paragraph p => p with { Content = newChildren[0] },
            _ => this
        };
    [GeneratedRegex("\\s+")]
    private static partial Regex Whitespaces();
}

internal abstract record Reference
{
    public sealed record Unresolved(Xref Xref) : Reference;

    public sealed record Resolved(string Title, Uri Url) : Reference;
}
