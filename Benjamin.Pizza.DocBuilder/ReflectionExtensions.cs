namespace Benjamin.Pizza.DocBuilder;

internal static class ReflectionExtensions
{
    public static bool IsDelegate(this Type type)
        => type.BaseType?.FullName is "System.MulticastDelegate" or "System.Delegate";
}
