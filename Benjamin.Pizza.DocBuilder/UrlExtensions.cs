namespace Benjamin.Pizza.DocBuilder;

internal static class UrlExtensions
{
    public static string UrlFriendlyName(string name)
        => name
            .Replace("``", "-", StringComparison.InvariantCulture)
            .Replace("`", "-", StringComparison.InvariantCulture);
}
