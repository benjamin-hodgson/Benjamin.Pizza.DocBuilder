using System.Collections.Immutable;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Sawmill;

namespace Benjamin.Pizza.DocBuilder;

internal abstract partial record Markup : IRewritable<Markup>
{
    public sealed record Nil : Markup;
    public sealed record Seq(ImmutableArray<Markup> Values) : Markup
    {
        public Seq(params Markup[] values)
            : this(values.ToImmutableArray())
        {
        }
    }

    public sealed record SectionHeader(Markup Title, int Level, string? Id) : Markup;
    public sealed record Paragraph(Markup Content) : Markup;
    public sealed record Text(string Value) : Markup;
    public sealed record Link(Reference Reference) : Markup;
    public sealed record InlineCode(string Code) : Markup;
    public sealed record CodeBlock(string Code) : Markup;
    public sealed record List(ImmutableArray<Markup> Items) : Markup;

    public static Markup FromXml(XNode node)
        => node switch
        {
            XText t => new Text(Whitespaces().Replace(t.Value, " ")),
            XElement e => e.Name.LocalName switch
            {
                "see" => new Link(new Reference.Unresolved(Xref.FromCref(e))),
                "para" => new Paragraph(new Seq(e.Nodes().Select(FromXml).ToImmutableArray())),
                "c" => new InlineCode(((XText)e.FirstNode!).Value),
                "code" => CreateCodeBlock(((XText)e.FirstNode!).Value),
                "paramref" or "typeparamref" => new InlineCode(e.Attribute("name")!.Value),
                "example" => Example(e),
                var n => new Text("???" + n)
            },
            var n => new Text($"???{n.NodeType}")
        };

    private static Markup Example(XElement e)
    {
        var name = e.Attribute("name")?.Value;
        var body = e.Nodes().Select(FromXml);

        var markup = string.IsNullOrEmpty(name)
            ? body
            : body.Prepend(new SectionHeader(new Text(name), 3, null));

        return new Seq(markup.ToImmutableArray());
    }

    public static Markup CreateCodeBlock(string code)
    {
        // trim the same amount of whitespace from each line (so as not to lose indentation)
        var lines = code.Split("\n").SkipWhile(string.IsNullOrWhiteSpace).ToList();

        var leadingWhitespace = lines[0].TakeWhile(char.IsWhiteSpace).Count();

        var trimmedLines = lines
            .Select(line => string.Concat(
                line
                    .Select((x, i) => (x, i))
                    .SkipWhile(t => char.IsWhiteSpace(t.x) && t.i < leadingWhitespace)
                    .Select(t => t.x)
                )
            );

        return new CodeBlock(string.Join("\n", trimmedLines).Trim());
    }

    public int CountChildren()
        => this switch
        {
            Seq(var children) => children.Length,
            Paragraph => 1,
            SectionHeader => 1,
            List(var children) => children.Length,
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
            case SectionHeader(var text, _, _):
                childrenReceiver[0] = text;
                break;
            case List(var children):
                children.CopyTo(childrenReceiver);
                break;
        }
    }

    public Markup SetChildren(ReadOnlySpan<Markup> newChildren)
        => this switch
        {
            Seq s => s with { Values = newChildren.ToImmutableArray() },
            Paragraph p => p with { Content = newChildren[0] },
            SectionHeader s => s with { Title = newChildren[0] },
            List l => l with { Items = newChildren.ToImmutableArray() },
            _ => this
        };
    [GeneratedRegex("\\s+")]
    private static partial Regex Whitespaces();

    public static implicit operator Markup(string text) => new Text(text);
}

internal abstract record Reference
{
    public sealed record Unresolved(Xref Xref) : Reference;

    public sealed record Resolved(string Title, Uri Url) : Reference;
}
