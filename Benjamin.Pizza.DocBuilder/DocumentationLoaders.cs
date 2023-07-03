using System.Reflection;
using System.Xml.Linq;

namespace Benjamin.Pizza.DocBuilder;

internal interface IDocumentationSectionLoader<TContext, TItem>
{
    Markup.SectionHeader SectionHeader { get; }
    IEnumerable<TItem> GetItems(TContext ctx);
    Xref GetXref(TItem item);
    DocumentationFragment Load(TItem item, XElement docElement);
}

internal sealed record DocumentationFragment(
    string Name,
    string UrlFragment,
    IEnumerable<Markup> Markup);

internal abstract class TypeDocumentationLoader : IDocumentationSectionLoader<IEnumerable<Type>, Type>
{
    public abstract Markup.SectionHeader SectionHeader { get; }

    public abstract IEnumerable<Type> GetItems(IEnumerable<Type> ctx);

    public Xref GetXref(Type type) => Xref.Create(type);

    public DocumentationFragment Load(Type type, XElement docElement)
        => new(
            type.FriendlyName(),
            type.UrlFriendlyName(),
            (docElement.Element("summary")?.Nodes() ?? Enumerable.Empty<XNode>())
                .Select(Markup.FromXml)
                .Prepend(new Markup.SectionHeader(new Markup.Link(new Reference.Unresolved(Xref.Create(type))), 3, null))
        );
}

internal sealed class ClassDocumentationLoader : TypeDocumentationLoader
{
    public override Markup.SectionHeader SectionHeader => new("Classes", 2, "classes");

    public override IEnumerable<Type> GetItems(IEnumerable<Type> types)
        => types.Where(t => t.IsClass && !t.IsDelegate());
}

internal sealed class InterfaceDocumentationLoader : TypeDocumentationLoader
{
    public override Markup.SectionHeader SectionHeader => new("Interfaces", 2, "interfaces");

    public override IEnumerable<Type> GetItems(IEnumerable<Type> types)
        => types.Where(t => t.IsInterface);
}

internal sealed class DelegateDocumentationLoader : TypeDocumentationLoader
{
    public override Markup.SectionHeader SectionHeader => new("Delegates", 2, "delegates");

    public override IEnumerable<Type> GetItems(IEnumerable<Type> types)
        => types.Where(t => t.IsDelegate());
}

internal sealed class EnumDocumentationLoader : TypeDocumentationLoader
{
    public override Markup.SectionHeader SectionHeader => new("Enums", 2, "enums");

    public override IEnumerable<Type> GetItems(IEnumerable<Type> types)
        => types.Where(t => t.IsEnum);
}

internal sealed class StructDocumentationLoader : TypeDocumentationLoader
{
    public override Markup.SectionHeader SectionHeader => new("Structs", 2, "structs");

    public override IEnumerable<Type> GetItems(IEnumerable<Type> types)
        => types.Where(t => t.IsValueType && !t.IsEnum);
}

internal abstract class MethodBaseDocumentationLoader<T> : IDocumentationSectionLoader<Type, T>
    where T : MethodBase
{
    public abstract Markup.SectionHeader SectionHeader { get; }

    public abstract IEnumerable<T> GetItems(Type type);

    public Xref GetXref(T meth) => Xref.Create(meth);

    public DocumentationFragment Load(T meth, XElement docElement)
        => new(
            meth.FriendlyName(),
            "#" + meth.UrlFriendlyName(),
            (docElement
                .Element("summary")?
                .Nodes()
                .Select(Markup.FromXml)
                ?? Enumerable.Empty<Markup>())
            .Prepend(new Markup.SectionHeader(meth.FriendlyName(), 3, meth.UrlFriendlyName()))
        );
}

internal sealed class ConstructorDocumentationLoader : MethodBaseDocumentationLoader<ConstructorInfo>
{
    public override Markup.SectionHeader SectionHeader => new("Constructors", 2, "constructors");

    public override IEnumerable<ConstructorInfo> GetItems(Type type)
        => type.IsDelegate()
            ? Array.Empty<ConstructorInfo>()
            : type.GetConstructors();
}

internal sealed class MethodDocumentationLoader : MethodBaseDocumentationLoader<MethodInfo>
{
    public override Markup.SectionHeader SectionHeader => new("Methods", 2, "methods");

    public override IEnumerable<MethodInfo> GetItems(Type type)
        => type.IsDelegate()
            ? Array.Empty<MethodInfo>()
            : type
                .GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                .Where(m => !m.IsSpecialName)
                .ToArray();
}

internal sealed class PropertyDocumentationLoader : IDocumentationSectionLoader<Type, PropertyInfo>
{
    public Markup.SectionHeader SectionHeader => new("Properties", 2, "properties");

    public IEnumerable<PropertyInfo> GetItems(Type type)
        => type
            .GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName);

    public Xref GetXref(PropertyInfo property) => Xref.Create(property);

    public DocumentationFragment Load(PropertyInfo property, XElement docElement)
        => new(
            property.FriendlyName(),
            "#" + property.UrlFriendlyName(),
            (docElement
                .Element("summary")?
                .Nodes()
                .Select(Markup.FromXml)
                ?? Enumerable.Empty<Markup>())
                .Prepend(new Markup.SectionHeader(property.FriendlyName(), 3, property.UrlFriendlyName()))
        );
}

internal sealed class FieldDocumentationLoader : IDocumentationSectionLoader<Type, FieldInfo>
{
    public Markup.SectionHeader SectionHeader => new("Fields", 2, "fields");

    public IEnumerable<FieldInfo> GetItems(Type type)
        => type
            .GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName);

    public Xref GetXref(FieldInfo property) => Xref.Create(property);

    public DocumentationFragment Load(FieldInfo field, XElement docElement)
        => new(
            field.FriendlyName(),
            "#" + field.UrlFriendlyName(),
            (docElement
                .Element("summary")?
                .Nodes()
                .Select(Markup.FromXml)
                ?? Enumerable.Empty<Markup>())
                .Prepend(new Markup.SectionHeader(field.FriendlyName(), 3, field.UrlFriendlyName()))
        );
}
