using System.Xml.Linq;

namespace Benjamin.Pizza.DocBuilder;

internal interface INamespaceDocumentationSection
{
    Markup.SectionHeader SectionHeader { get; }
    bool ShouldIncludeType(Type types);
    IEnumerable<Markup> GetMarkup(Type item, XElement docElement);
}

internal abstract class TypeDocumentationSection : INamespaceDocumentationSection
{
    public abstract Markup.SectionHeader SectionHeader { get; }

    public abstract bool ShouldIncludeType(Type type);

    public IEnumerable<Markup> GetMarkup(Type type, XElement docElement)
        => (docElement.Element("summary")?.Nodes() ?? Enumerable.Empty<XNode>())
                .Select(Markup.FromXml)
                .Prepend(new Markup.SectionHeader(new Markup.Link(new Reference.Unresolved(Xref.Create(type))), 3, null));
}

internal sealed class ClassDocumentationSection : TypeDocumentationSection
{
    public override Markup.SectionHeader SectionHeader => new("Classes", 2, "classes");

    public override bool ShouldIncludeType(Type type)
        => type.IsClass && !type.IsDelegate();
}

internal sealed class InterfaceDocumentationSection : TypeDocumentationSection
{
    public override Markup.SectionHeader SectionHeader => new("Interfaces", 2, "interfaces");

    public override bool ShouldIncludeType(Type type)
        => type.IsInterface;
}

internal sealed class DelegateDocumentationSection : TypeDocumentationSection
{
    public override Markup.SectionHeader SectionHeader => new("Delegates", 2, "delegates");

    public override bool ShouldIncludeType(Type type)
        => type.IsDelegate();
}

internal sealed class EnumDocumentationSection : TypeDocumentationSection
{
    public override Markup.SectionHeader SectionHeader => new("Enums", 2, "enums");

    public override bool ShouldIncludeType(Type type)
        => type.IsEnum;
}

internal sealed class StructDocumentationSection : TypeDocumentationSection
{
    public override Markup.SectionHeader SectionHeader => new("Structs", 2, "structs");

    public override bool ShouldIncludeType(Type type)
        => type.IsValueType && !type.IsEnum;
}
