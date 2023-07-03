using System.Reflection;
using System.Xml.Linq;

namespace Benjamin.Pizza.DocBuilder;

internal interface IDocumentationSectionLoader<T>
{
    Markup.SectionHeader SectionHeader { get; }
    IEnumerable<T> GetItems(Type type);
    Xref GetXref(T item);
    LoadedDocumentationFragment Load(T item, XElement docElement);
}

internal sealed record LoadedDocumentationFragment(
    string Name,
    string UrlFragment,
    IEnumerable<Markup> Markup);

internal abstract class MethodBaseDocumentationLoader<T> : IDocumentationSectionLoader<T>
    where T : MethodBase
{
    public abstract Markup.SectionHeader SectionHeader { get; }

    public abstract IEnumerable<T> GetItems(Type type);

    public Xref GetXref(T meth) => Xref.Create(meth);

    public LoadedDocumentationFragment Load(T meth, XElement docElement)
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
    private ConstructorDocumentationLoader()
    {
    }

    public override Markup.SectionHeader SectionHeader => new("Constructors", 2, "constructors");

    public override IEnumerable<ConstructorInfo> GetItems(Type type)
        => type.IsDelegate()
            ? Array.Empty<ConstructorInfo>()
            : type.GetConstructors();

    public static IDocumentationSectionLoader<ConstructorInfo> Instance { get; }
        = new ConstructorDocumentationLoader();
}

internal sealed class MethodDocumentationLoader : MethodBaseDocumentationLoader<MethodInfo>
{
    private MethodDocumentationLoader()
    {
    }

    public override Markup.SectionHeader SectionHeader => new("Methods", 2, "methods");

    public override IEnumerable<MethodInfo> GetItems(Type type)
        => type.IsDelegate()
            ? Array.Empty<MethodInfo>()
            : type
                .GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                .Where(m => !m.IsSpecialName)
                .ToArray();

    public static IDocumentationSectionLoader<MethodInfo> Instance { get; }
        = new MethodDocumentationLoader();
}

internal sealed class PropertyDocumentationLoader : IDocumentationSectionLoader<PropertyInfo>
{
    private PropertyDocumentationLoader()
    {
    }

    public Markup.SectionHeader SectionHeader => new("Properties", 2, "properties");

    public IEnumerable<PropertyInfo> GetItems(Type type)
        => type
            .GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName);

    public Xref GetXref(PropertyInfo property) => Xref.Create(property);

    public LoadedDocumentationFragment Load(PropertyInfo property, XElement docElement)
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

    public static IDocumentationSectionLoader<PropertyInfo> Instance { get; }
        = new PropertyDocumentationLoader();
}
