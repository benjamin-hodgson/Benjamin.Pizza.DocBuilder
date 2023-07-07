using System.Reflection;
using System.Text.RegularExpressions;

namespace Benjamin.Pizza.DocBuilder;

internal static partial class NameExtensions
{
    public static string FriendlyName(this Type type, bool includeTypeParams = true)
    {
        var typeParams = type.IsGenericType && includeTypeParams
            ? $"<{string.Join(", ", type.GetGenericArguments().Select(t => t.FriendlyName()))}>"
            : "";

        return type.FullName switch
        {
            "System.Void" => "void",
            "System.Boolean" => "bool",
            "System.Byte" => "byte",
            "System.SByte" => "sbyte",
            "System.Char" => "char",
            "System.Int16" => "short",
            "System.UInt16" => "ushort",
            "System.Int32" => "int",
            "System.UInt32" => "uint",
            "System.Int64" => "long",
            "System.UInt64" => "ulong",
            "System.IntPtr" => "nint",
            "System.UIntPtr" => "nuint",
            "System.Float" => "float",
            "System.Double" => "double",
            "System.Decimal" => "decimal",
            "System.Object" => "object",
            "System.String" => "string",
            _ when type.IsGenericType => GenericTypeSuffix().Replace(type.Name, "") + typeParams,
            _ => type.Name
        };
    }

    public static string FriendlyName(this MethodBase meth, bool includeTypeParams = true, bool includeParams = true)
    {
        var typeParams = meth.IsGenericMethod && includeTypeParams
            ? $"<{string.Join(", ", meth.GetGenericArguments().Select(t => t.FriendlyName()))}>"
            : "";

        var prefix = meth.IsGenericMethodDefinition
            ? $"{GenericTypeSuffix().Replace(meth.Name, "")}"
            : meth.Name;

        var parameters = includeParams
            ? "(" + string.Join(", ", meth.GetParameters().Select(FriendlyName)) + ")"
            : "";

        return prefix + typeParams + parameters;
    }

    public static string FriendlyName(this ParameterInfo param)
        => param.ParameterType.FriendlyName() + " " + param.Name;

    public static string FriendlyName(this PropertyInfo prop)
        => prop.Name;

    public static string FriendlyName(this FieldInfo field)
        => field.Name;

    [GeneratedRegex(@"(`|``)\d+")]
    private static partial Regex GenericTypeSuffix();
}
