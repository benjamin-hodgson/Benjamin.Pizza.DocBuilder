using System.Reflection;
using System.Text.RegularExpressions;

namespace Benjamin.Pizza.DocBuilder;

internal static partial class NameExtensions
{
    public static string FriendlyName(this Type type)
        => type.FullName switch
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
            _ when type.IsGenericType => $"{GenericTypeSuffix().Replace(type.Name, "")}<{string.Join(", ", type.GetGenericArguments().Select(FriendlyName))}>",
            _ => type.Name
        };

    public static string FriendlyName(this MethodBase meth)
    {
        var prefix = meth.IsGenericMethodDefinition
            ? $"{GenericTypeSuffix().Replace(meth.Name, "")}<{string.Join(", ", meth.GetGenericArguments().Select(FriendlyName))}>"
            : meth.Name;
        var parameters = meth.GetParameters().Select(FriendlyName);
        return prefix + '(' + string.Join(", ", parameters) + ')';
    }

    public static string FriendlyName(this ParameterInfo param)
        => param.ParameterType.FriendlyName() + " " + param.Name;

    public static string FriendlyName(this PropertyInfo prop)
        => prop.Name;

    [GeneratedRegex(@"(`|``)\d+")]
    private static partial Regex GenericTypeSuffix();
}
