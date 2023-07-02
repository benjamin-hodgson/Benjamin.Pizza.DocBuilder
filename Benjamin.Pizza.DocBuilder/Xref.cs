using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Benjamin.Pizza.DocBuilder;

internal partial record struct Xref(string Value)
{
    public static Xref FromName(XElement xml)
        => new (xml.Attribute("name")?.Value ?? throw new InvalidOperationException(xml.ToString()));

    public static Xref FromCref(XElement xml)
        => new (xml.Attribute("cref")?.Value ?? throw new InvalidOperationException(xml.ToString()));

    public static Xref Create(Type type)
        => new("T:" + GetTypeXrefName(type));

    public static Xref Create(PropertyInfo prop)
        => new(GetXref(prop, 'P'));

    public static Xref Create(FieldInfo field)
        => new(GetXref(field, 'F'));

    public static Xref Create(MethodBase meth)
    {
        var paramTypes = meth
            .GetParameters()
            .Select(p => GetParamTypeName(p.ParameterType))
            .ToList();

        var genericSuffix = meth.IsGenericMethodDefinition
            ? "``" + meth.GetGenericArguments().Length
            : "";

        var paramsSuffix = paramTypes.Any()
            ? $"({string.Join(',', paramTypes)})"
            : "";

        var name = meth.Name == ".ctor" ? ".#ctor" : meth.Name;

        return new($"M:{GetTypeXrefName(meth.DeclaringType!)}.{name}{genericSuffix}{paramsSuffix}");
    }

    private static string GetParamTypeName(Type paramType)
    {
        if (paramType.HasElementType)
        {
            var suffix = paramType.IsArray
                ? "[]"
                : paramType.IsPointer
                    ? "*"
                    : "@"; /* paramType.IsByRef */

            return GetParamTypeName(paramType.GetElementType()!) + suffix;
        }

        if (paramType.IsGenericParameter)
        {
            var prefix = paramType.DeclaringMethod != null ? "``" : "`";
            return prefix + paramType.GenericParameterPosition;
        }

        if (paramType.IsConstructedGenericType)
        {
            var name = Regex.Replace(paramType.GetGenericTypeDefinition().FullName!, @"(`|``)\d+", "");
            return name + '{' + string.Join(',', paramType.GenericTypeArguments.Select(GetParamTypeName)) + '}';
        }

        return GetTypeXrefName(paramType);
    }

    private static string GetXref(MemberInfo member, char prefix)
        => $"{prefix}:{GetTypeXrefName(member.DeclaringType!)}.{member.Name}";

    private static string GetTypeXrefName(Type type)
    {
        var name = type.FullName ?? type.GetGenericTypeDefinition().FullName;

        return FixTypeNameRegex().Replace(name!, "").Replace('+', '.');
    }

    // "stuff between angle brackets"
    [GeneratedRegex(@"\[.*\]")]
    private static partial Regex FixTypeNameRegex();
}
