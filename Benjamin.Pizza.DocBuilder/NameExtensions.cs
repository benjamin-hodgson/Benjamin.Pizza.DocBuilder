using System.Reflection;
using System.Text.RegularExpressions;

namespace Benjamin.Pizza.DocBuilder;

internal static partial class NameExtensions
{
    public static string FriendlyName(this Type type)
        => type.IsGenericTypeDefinition
            ? $"{GenericTypeSuffix().Replace(type.Name, "")}<{string.Join(", ", type.GetGenericArguments().Select(FriendlyName))}>"
            : type.Name;

    public static string FriendlyName(this MethodBase meth)
        => meth.IsGenericMethodDefinition
            ? $"{GenericTypeSuffix().Replace(meth.Name, "")}<{string.Join(", ", meth.GetGenericArguments().Select(FriendlyName))}>"
            : meth.Name;

    [GeneratedRegex(@"(`|``)\d+")]
    private static partial Regex GenericTypeSuffix();
}
