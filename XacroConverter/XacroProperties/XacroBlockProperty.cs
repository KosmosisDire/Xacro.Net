

using System.Xml;
using static IXacroProperty;

public class XacroBlockProperty : IXacroProperty
{
    public string Name { get; set; }
    public List<XmlNode> Value { get; set; }
    
    public PropertyType Type => PropertyType.Block;
    object IXacroProperty.Value
    {
        get => Value;
        set => Value = (List<XmlNode>)value;
    
    }

    public XacroBlockProperty(string name, List<XmlNode> value)
    {
        Name = name;
        Value = value;
    }
}