using Eighty;

using Sawmill;

using static Eighty.Html;

namespace Benjamin.Pizza.DocBuilder;

internal static class HtmlRenderer
{
    public static Html Render(DocumentationPage page)
        => doctypeHtml_(
            head_(
                meta(new Attr("http-equiv", "Content-Type"), new Attr("content", "text/html; charset=UTF-8")),
                meta(new Attr("name", "viewport"), new Attr("content", "width=device-width, initial-scale=1")),
                title_(page.Title + " - benjamin.pizza"),
                link(
                    new Attr("rel", "stylesheet"),
                    new Attr("href", "https://cdn.jsdelivr.net/npm/water.css@2.1.1/out/water.min.css"),
                    Attr.Raw("integrity", "sha256-QST90Wzz4PEr5KlclQaOCsjc00FTyf86Wrj41oqZB4w="),
                    new Attr("crossorigin", "anonymous")
                ),
                link(
                    new Attr("rel", "stylesheet"),
                    new Attr("href", "https://www.benjamin.pizza/Pidgin/v3.2.1/css/highlight.css"),
                    Attr.Raw("integrity", "sha256-PauBCvnoQpib8ARwqINekN+l+9Xp3bzpLqDfsOOeT2Y="),
                    new Attr("crossorigin", "anonymous")
                ),
                script(
                    new Attr("id", "highlightjs-script"),
                    new Attr("src", "https://www.benjamin.pizza/Pidgin/v3.2.1/js/third-party/highlight.js"),
                    Attr.Raw("integrity", "sha256-MkDBSftFSewS+sILF5R37X9a61pVh4CiGj9pCatL0mE="),
                    new Attr("crossorigin", "anonymous"),
                    new Attr("async")
                ),
                script_(Raw("if (window.hljs) { hljs.highlightAll(); } else { document.getElementById(\"highlightjs-script\").onload = () => { hljs.highlightAll(); }; }"))
            ),
            body_(
                h1_(page.Title),
                _(Render(page.Body))
            )
        );

    public static Html Render(Markup markup)
        => markup.Fold<Markup, Html>((children, item) => item switch
        {
            Markup.Seq => _(children.ToArray()),
            Markup.Paragraph => p_(children[0]),
            Markup.Text(var t) => Text(t),
            Markup.SectionHeader(_, var level, null) => Tag("h" + level)._(children[0]),
            Markup.SectionHeader(_, var level, var id) => Tag("h" + level, new Attr("id", id))._(children[0]),
            Markup.Link(Reference.Resolved(var t, var href)) => a(href: href.ToString())._(t),
            Markup.Link(Reference.Unresolved(Xref(var xref))) => a(href: xref, style: "color: red")._(xref),
            Markup.InlineCode(var code) => code_(code),
            Markup.CodeBlock(var c) => pre_(code(@class: "lang-csharp hljs")._(c)),
            Markup.List => ul_(children.ToArray().Select(li_)),
            _ => throw new ArgumentOutOfRangeException(nameof(markup), item, "Unknown markup")
        });
}
