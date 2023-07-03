using System.Collections.Immutable;
using System.Xml.Linq;

namespace Benjamin.Pizza.DocBuilder;

internal sealed class XmlDocFile
{
    private readonly ImmutableDictionary<Xref, XElement> _members;
    private XmlDocFile(ImmutableDictionary<Xref, XElement> members)
    {
        _members = members;
    }

    public XElement GetDoc(Xref xref) => _members[xref];

    public XElement GetDoc(Type type) => GetDoc(Xref.Create(type));

    public static XmlDocFile Load(string file)
    {
        var doc = XDocument.Load(file);
        var members = doc
            .Element("doc")!
            .Element("members")!
            .Elements()
            .ToImmutableDictionary(Xref.FromName);

        return new XmlDocFile(members);
    }
}
