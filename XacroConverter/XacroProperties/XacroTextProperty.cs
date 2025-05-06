


using static IXacroProperty;

public class XacroTextProperty : IXacroProperty
{
    public string Name { get; set; }
    public string Value { get; set; }

    public PropertyType Type => PropertyType.Text;
    object IXacroProperty.Value
    {
        get => Value;
        set => Value = (string)value;
    }

    public XacroTextProperty(string name, string value)
    {
        Name = name;
        Value = value;
    }
}