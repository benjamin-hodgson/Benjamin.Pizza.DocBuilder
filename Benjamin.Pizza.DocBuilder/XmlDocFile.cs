using System.Collections.Immutable;
using System.Reflection;
using System.Xml.Linq;

namespace Benjamin.Pizza.DocBuilder;

internal sealed class XmlDocFile
{
    private readonly ImmutableDictionary<Xref, XElement> _members;
    private XmlDocFile(ImmutableDictionary<Xref, XElement> members)
    {
        _members = members;
    }

    public XElement GetDoc(Type type) => _members[Xref.Create(type)];
    public XElement GetDoc(PropertyInfo prop) => _members[Xref.Create(prop)];
    public XElement GetDoc(FieldInfo field) => _members[Xref.Create(field)];
    public XElement GetDoc(MethodBase meth) => _members[Xref.Create(meth)];

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
