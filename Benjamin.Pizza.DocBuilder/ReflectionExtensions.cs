using System.Reflection;

namespace Benjamin.Pizza.DocBuilder;

internal static class ReflectionExtensions
{
    public static bool IsDelegate(this Type type)
        => type.BaseType?.FullName is "System.MulticastDelegate" or "System.Delegate";

    public static string GetDeclaration(this Type type)
    {
        var visibility = type.IsNestedFamORAssem || type.IsNestedFamily ? "protected " : "";

        if (type.IsEnum)
        {
            var underlying = type.GetEnumUnderlyingType();
            return visibility
                + "enum "
                + type.Name
                + (underlying.FullName == "System.Int32" ? "" : " : " + underlying.FriendlyName());
        }

        var (typeParams, whereClause) = GetGenericParametersDeclaration(type.GetGenericArguments());

        if (type.IsDelegate())
        {
            var invokeMethod = type.GetMethod("Invoke")!;
            var parameters = string.Join(", ", invokeMethod.GetParameters().Select(p => p.ParameterType.FriendlyName() + " " + p.Name));

            return visibility
                + "delegate "
                + invokeMethod.ReturnType.FriendlyName()
                + " "
                + type.FriendlyName(includeTypeParams: false)
                + typeParams
                + "(" + GetParametersDeclaration(invokeMethod) + ")"
                + whereClause;
        }

        var kwds = type.IsInterface
            ? "interface"
            : type.IsValueType
                ? "struct"
                : (type.IsAbstract, type.IsSealed) switch
                {
                    (true, true) => "static class",
                    (true, false) => "abstract class",
                    (false, true) => "sealed class",
                    (false, false) => "class"
                };

        var inheritance = new List<string>();
        if (!type.IsValueType && type.BaseType != null && type.BaseType!.FullName != "System.Object")
        {
            inheritance.Add(type.BaseType!.FriendlyName());
        }

        inheritance.AddRange(type.GetInterfaces().Select(i => i.FriendlyName()));

        return visibility
            + kwds + " "
            + type.FriendlyName(includeTypeParams: false)
            + typeParams
            + (inheritance.Any() ? " : " + string.Join(", ", inheritance) : "")
            + whereClause;
    }

    public static string GetDeclaration(this MethodInfo meth)
    {
        var visibility = meth.IsFamilyOrAssembly || meth.IsFamily ? "protected " : "";

        // todo: override? may not be possible
        var kwd = meth.IsStatic
            ? "static "
            : meth.DeclaringType!.IsInterface
                ? ""
                : meth.IsAbstract
                    ? "abstract "
                    : meth.IsVirtual && !meth.IsFinal
                        ? "virtual "
                        : "";

        var (typeParams, whereClause) = GetGenericParametersDeclaration(meth.GetGenericArguments());

        return visibility
            + kwd
            + meth.ReturnType.FriendlyName()
            + " "
            + meth.FriendlyName(includeTypeParams: false, includeParams: false)
            + typeParams +
            "(" + GetParametersDeclaration(meth) + ")"
            + whereClause;
    }

    public static string GetDeclaration(this ConstructorInfo meth)
    {
        var visibility = meth.IsFamilyOrAssembly || meth.IsFamily ? "protected " : "";
        var kwds = meth.IsStatic ? "static " : "";
        return visibility
            + kwds
            + meth.DeclaringType!.FriendlyName(includeTypeParams: false)
            + "(" + GetParametersDeclaration(meth) + ")";
    }

    private static (string typeParams, string whereClause) GetGenericParametersDeclaration(Type[] genericParameters)
    {
        var typeParams = new List<string>();
        var whereClause = new List<string>();

        foreach (var param in genericParameters)
        {
            var variance = (param.GenericParameterAttributes & GenericParameterAttributes.VarianceMask) switch
            {
                GenericParameterAttributes.Covariant => "out ",
                GenericParameterAttributes.Contravariant => "in ",
                _ => ""
            };
            typeParams.Add(variance + param.Name);

            var constraints = new List<string>();
            if ((param.GenericParameterAttributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0)
            {
                constraints.Add("class");
            }

            if ((param.GenericParameterAttributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
            {
                constraints.Add("struct");
            }

            if ((param.GenericParameterAttributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0)
            {
                constraints.Add("new()");
            }

            constraints.AddRange(param.GetGenericParameterConstraints().Select(t => t.FriendlyName()));

            if (constraints.Any())
            {
                whereClause.Add($"where {param.FriendlyName()} : {string.Join(", ", constraints)}");
            }
        }

        var typeParamsStr = typeParams.Any()
            ? "<" + string.Join(", ", typeParams) + ">"
            : "";
        var whereClauseStr = whereClause.Any()
            ? "\n    " + string.Join("\n    ", whereClause)
            : "";
        return (typeParamsStr, whereClauseStr);
    }

    private static string GetParametersDeclaration(MethodBase meth)
    {
        var isExtension = meth
            .GetCustomAttributesData()
            .Select(a => a.AttributeType.FullName)
            .Contains("System.Runtime.CompilerServices.ExtensionAttribute");

        // todo: params[]; default
        static string GetDeclaration(ParameterInfo param)
        {
            var prefix = param.IsIn
                ? "in "
                : param.IsOut
                    ? "out "
                    : param.ParameterType.IsByRef
                        ? "ref "
                        : "";

            var defaultValue = param.HasDefaultValue
                ? " = " + param.DefaultValue!.ToString()
                : "";

            return prefix + param.ParameterType.FriendlyName() + " " + param.Name;
        }

        return (isExtension ? "this " : "") + string.Join(", ", meth.GetParameters().Select(GetDeclaration));
    }
}
