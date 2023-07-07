using Eighty;
using static Eighty.Html;

namespace Benjamin.Pizza.DocBuilder;

internal static class RedirectPage
{
    public static Html GetHtml(Uri redirectUri)
        => doctypeHtml_(
            head_(
                meta(charset: "UTF-8"),
                meta(new Attr("http-equiv", "refresh"), new Attr("content", $"0; url={redirectUri}")),
                title_("Page Redirection"),
                script(type: "text/javascript")._($"window.location.href = \"{redirectUri}\";")
            ),
            body_("Taking you to ", a(href: redirectUri.ToString())._("the docs"), "...")
        );
}
