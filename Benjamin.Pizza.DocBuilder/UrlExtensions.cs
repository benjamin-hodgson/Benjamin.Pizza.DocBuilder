using System.Reflection;

namespace Benjamin.Pizza.DocBuilder;

internal static class UrlExtensions
{
    public static string UrlFriendlyName(this Type type)
        => MakeUrlFriendly(type.FullName!);

    public static string UrlFriendlyName(this MethodBase meth)
        => MakeUrlFriendly(meth.Name) + "__" + string.Join('_', meth.GetParameters().Select(p => MakeUrlFriendly(p.ParameterType.Name)));

    public static string UrlFriendlyName(this PropertyInfo prop)
        => MakeUrlFriendly(prop.Name);

    public static string MakeUrlFriendly(string name)
        => name
            .Replace("``", "-", StringComparison.InvariantCulture)
            .Replace("`", "-", StringComparison.InvariantCulture);
}
