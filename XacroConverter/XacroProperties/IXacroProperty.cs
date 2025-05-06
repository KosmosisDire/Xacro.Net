

using System.Xml;

public interface IXacroProperty
{
    string Name { get; }
    object Value { get; set; }
    PropertyType Type { get; }
    public enum PropertyType
    {
        Text,
        Block
    }

    public static IXacroProperty CreateProperty(XmlElement propertyDefinition)
    {
        var name = propertyDefinition.GetAttribute("name");
        if (name.StartsWith('*'))
        {
            return new XacroBlockProperty(name, propertyDefinition.ChildNodes.Count > 0 ? propertyDefinition.ChildNodes.Cast<XmlNode>().ToList() : []);
        }
        else
        {
            return new XacroTextProperty(name, propertyDefinition.GetAttribute("value") ?? "");
        }
    }
}