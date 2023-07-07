using System.Reflection;
using System.Xml.Linq;

namespace Benjamin.Pizza.DocBuilder;

internal interface ITypeDocumentationSection<TItem>
{
    Markup.SectionHeader SectionHeader { get; }
    IEnumerable<TItem> GetItems(Type type);
    Xref GetXref(TItem item);
    DocumentationFragment Load(TItem item, XElement docElement);
}

internal sealed record DocumentationFragment(
    string Name,
    string UrlFragment,
    IEnumerable<Markup> Markup);

internal abstract class MethodBaseDocumentationSection<T> : ITypeDocumentationSection<T>
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

internal sealed class ConstructorDocumentationSection : MethodBaseDocumentationSection<ConstructorInfo>
{
    public override Markup.SectionHeader SectionHeader => new("Constructors", 2, "constructors");

    public override IEnumerable<ConstructorInfo> GetItems(Type type)
        => type.IsDelegate()
            ? Array.Empty<ConstructorInfo>()
            : type.GetConstructors();
}

internal sealed class MethodDocumentationSection : MethodBaseDocumentationSection<MethodInfo>
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

internal sealed class PropertyDocumentationSection : ITypeDocumentationSection<PropertyInfo>
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

internal sealed class FieldDocumentationSection : ITypeDocumentationSection<FieldInfo>
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
