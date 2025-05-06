
using System.Text.RegularExpressions;
using System.Xml;

public class XacroMacroContext
{
    public string Name { get; set; }
    public Dictionary<string, IXacroProperty> Params { get; init; } = [];
    public List<XmlNode> Body { get; set; }
    public XmlElement MacroElement { get; set; }
    public Dictionary<string, IXacroProperty> GlobalProperties { get; init; } = [];
    public Dictionary<string, IXacroProperty> LocalProperties { get; init; } = [];
    public Dictionary<string, XacroMacroContext> GlobalMacros { get; init; } = [];
    public Dictionary<string, XacroMacroContext> ChildMacros { get; init; } = [];
    public Dictionary<string, IXacroProperty> AllAvailableProperties => GlobalProperties.Concat(LocalProperties).ToDictionary(entry => entry.Key, entry => entry.Value).Concat(Params).ToDictionary(entry => entry.Key, entry => entry.Value);
    private readonly string rootPath;

    public XacroMacroContext(XmlElement macroDefinition, string rootPath, Dictionary<string, IXacroProperty> globalProperties, Dictionary<string, XacroMacroContext> globalMacros, Dictionary<string, IXacroProperty> inheritedProperties)
    {
        if (!Path.IsPathRooted(rootPath))
        {
            throw new ArgumentException("Root path must be an absolute path", nameof(rootPath));
        }

        // get the name and parameters of the macro
        MacroElement = macroDefinition;
        Name = macroDefinition.GetAttribute("name");
        var paramNames = macroDefinition.GetAttribute("params").Split(' ');
        Body = macroDefinition.ChildNodes.Cast<XmlNode>().ToList();

        Console.WriteLine($"Processing macro {Name}");

        for (int i = 0; i < paramNames.Length; i++)
        {
            var paramName = paramNames[i];
            if (paramName.StartsWith('*'))
                Params.Add(paramName, new XacroBlockProperty(paramName, []));
            else
                Params.Add(paramName, new XacroTextProperty(paramName, ""));
        }
        
        // init
        this.rootPath = rootPath;
        GlobalProperties = globalProperties;
        GlobalMacros = globalMacros;
        LocalProperties = inheritedProperties.ToDictionary(entry => entry.Key, entry => entry.Value); // Copy the dictionary

        // get a list of all includes and populate the child contexts
        ProcessIncludes();

        // set the local properties and macros
        GrabProperties();
        GrabMacros();

        // expand all child contexts into this one
        // ExpandMacros();

        // evaluate all properties and macros
        // EvalAll(macroDefinition);
    }

    private void GrabMacros()
    {
        var macroElements = MacroElement.GetElementsByTagName("xacro:macro").Cast<XmlElement>().ToList();
        macroElements.AddRange(MacroElement.GetElementsByTagName("macro").Cast<XmlElement>());
        foreach (var macro in macroElements)
        {
            var macroObj = new XacroMacroContext(macro, rootPath, GlobalProperties, GlobalMacros, LocalProperties);
            GlobalMacros[macroObj.Name] = macroObj;
            ChildMacros[macroObj.Name] = macroObj;
        }
    }

    private void GrabProperties()
    {
        var propertyElements = MacroElement.GetElementsByTagName("xacro:property").Cast<XmlElement>().ToList();
        propertyElements.AddRange(MacroElement.GetElementsByTagName("property").Cast<XmlElement>());
        foreach (var property in propertyElements)
        {
            var prop = IXacroProperty.CreateProperty(property);
            var isGlobal = !property.Name.StartsWith("xacro:");
            if (isGlobal)
                GlobalProperties[prop.Name] = prop;
            else
                LocalProperties[prop.Name] = prop;
        }
    }

    private void ProcessIncludes()
    {
        var includes = MacroElement.GetElementsByTagName("xacro:include").Cast<XmlElement>().ToList();
        includes.AddRange(MacroElement.GetElementsByTagName("include").Cast<XmlElement>());

        foreach (var include in includes)
        {
            string filename = EvalText(include.GetAttribute("filename"), LocalProperties);
            
            // Handle ROS package-relative paths
            if (filename.StartsWith("$(find "))
            {
                // In a real ROS environment, you'd resolve this path
                // However here, let's assume it's in the same directory as the input file
                var searchRegex = @"\$\(find(.+?)\)";
                var searchName = Regex.Match(filename, searchRegex).Groups[1].Value.Trim();
                var afterFind = Regex.Replace(filename, @"\$\(find.+?\)", "").Trim().Substring(1); // remove the $(find ...)/ part

                var fromRoot = Regex.Replace(filename, @"\$\(find.+?\)", rootPath);

                // if the file doesn't exist there then search for it up one folder
                if (!File.Exists(fromRoot))
                {
                    var parentPath = Path.GetDirectoryName(rootPath) ?? rootPath;
                    var folders = Directory.GetDirectories(parentPath, searchName, SearchOption.AllDirectories);
                    Console.WriteLine($"Searching for {searchName} in {parentPath}");
                    if (folders.Length > 0)
                    {
                        var folder = folders.ToList().Find(dir => 
                        {
                            var path = Path.Combine(dir, afterFind);
                            Console.WriteLine($"Checking {path}");
                            return File.Exists(path);
                        });
                        if (folder != null)
                        {
                            filename = Path.Combine(folder, afterFind);
                        }
                    }
                }
                else
                {
                    filename = fromRoot;
                }
            }
            
            if (!Path.IsPathRooted(filename))
            {
                filename = Path.Combine(rootPath, filename);
            }

            if (File.Exists(filename))
            {
                string includeContent = File.ReadAllText(filename);
                XmlDocument includedDoc = new XmlDocument();
                includedDoc.LoadXml(includeContent);
                // replace the include element with the contents of the included file
                foreach (XmlNode node in includedDoc.DocumentElement.ChildNodes)
                {
                    include.ParentNode?.InsertBefore(MacroElement.OwnerDocument.ImportNode(node, true), include);
                }
                include.ParentNode?.RemoveChild(include);

                ProcessIncludes();
            }
            else
            {
                Console.WriteLine($"Warning: Include file not found: {filename}");
            }

            // Remove the include element even if the file was not found, to avoid infinite loops
            include.ParentNode?.RemoveChild(include);
        }
    }

    private void EvalAll(XmlElement element)
    {
        // Evaluate attributes
        foreach (XmlAttribute attr in element.Attributes)
        {
            attr.Value = EvalText(attr.Value, AllAvailableProperties);
        }

        // Evaluate child elements
        for (int i = 0; i < element.ChildNodes.Count; i++)
        {
            XmlNode? child = element.ChildNodes[i];
            
            if (child is XmlElement childElement)
            {
                if (childElement.Name == "xacro:insert-block")
                {
                    string blockName = childElement.GetAttribute("name");
                    if (AllAvailableProperties.TryGetValue(blockName, out IXacroProperty? block))
                    {
                        if (block is XacroBlockProperty blockProp)
                        {
                            foreach (XmlNode node in blockProp.Value)
                            {
                                element.InsertBefore(node, child);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Attempted to insert block {blockName}, but it is not a block property");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Attempted to insert block {blockName}, but it was not found");
                    }

                    element.RemoveChild(childElement);
                    continue;
                }

                EvalAll(childElement);
            }
            else if (child is XmlText textNode)
            {
                textNode.Value = EvalText(textNode.Value ?? "", AllAvailableProperties);
            }
        }
    }

    private static string EvalText(string text, Dictionary<string, IXacroProperty> properties)
    {
        var textProperties = properties.Where(prop => prop.Value is XacroTextProperty).ToDictionary(prop => prop.Key, prop => ((XacroTextProperty)prop.Value).Value);
        foreach (var prop in textProperties)
        {
            text = text.Replace("${" + prop.Key + "}", prop.Value);

            // do math operations
            text = Regex.Replace(text, @"\$\{(\d+)([+\-*/])(\d+)\}", match =>
            {
                double a = double.Parse(match.Groups[1].Value);
                double b = double.Parse(match.Groups[3].Value);
                string op = match.Groups[2].Value;
                return op switch
                {
                    "+" => (a + b).ToString(),
                    "-" => (a - b).ToString(),
                    "*" => (a * b).ToString(),
                    "/" => (a / b).ToString(),
                    "^" => Math.Pow(a, b).ToString(),
                    _ => throw new InvalidOperationException("Invalid operator"),
                };
            });

            // unit conversion ${radians(-180)}
            text = Regex.Replace(text, @"\$\{(\w+)\(([^)]+)\)\}", match =>
            {
                string func = match.Groups[1].Value;
                string arg = match.Groups[2].Value;
                return func switch
                {
                    "radians" => (double.Parse(arg) * Math.PI / 180).ToString(),
                    "degrees" => (double.Parse(arg) * 180 / Math.PI).ToString(),
                    _ => throw new InvalidOperationException("Invalid function"),
                };
            });
        }
        return text;
    }

    // pull in the contents of the child contexts
    public void ExpandMacros()
    {
        foreach (var macro in GlobalMacros.Values)
        {
            if (macro == this)
            {
                continue;
            }

            if (macro.ChildMacros.ContainsValue(macro))
            {
                macro.ExpandMacros();
            }

            var refName = "xacro:" + macro.Name;
            var refElements = MacroElement.GetElementsByTagName(refName).Cast<XmlElement>().ToList();

            var paramNames = macro.Params.Keys;
            foreach (var refElement in refElements)
            {
                var paramInputs = new Dictionary<string, IXacroProperty>();
                foreach (var paramName in paramNames)
                {
                    var paramValue = refElement.GetAttribute(paramName);
                    if (paramName.StartsWith("**"))
                    {
                        var blockName = paramName[2..];
                        var blocks = refElement.GetElementsByTagName(blockName);
                        if (blocks.Count > 0)
                        {
                            var block = blocks[0];
                            var blockNodes = block?.ChildNodes.Cast<XmlNode>().ToList() ?? [];

                            paramInputs[paramName] = new XacroBlockProperty(paramName, blockNodes);

                            // var param = macro.Params[paramName];
                            // if (blockNodes != null && param is XacroBlockProperty blockProp)
                            // {
                            //     blockProp.Value = blockNodes;
                            // }
                        }
                    }
                    else if (paramName.StartsWith('*'))
                    {
                        var block = refElement.ChildNodes.Cast<XmlNode>().FirstOrDefault();
                        var param = macro.Params[paramName];

                        if (block != null && param is XacroBlockProperty blockProp)
                        {
                            blockProp.Value = [block];
                        }

                        // if (param is XacroBlockProperty blockProp)
                        // {
                        //     blockProp.Value = [block];
                        // }
                    }
                    else if (!string.IsNullOrEmpty(paramValue))
                    {
                        paramInputs[paramName] = new XacroTextProperty(paramName, paramValue);
                        // if (macro.Params[paramName] is XacroTextProperty textProp)
                        // {
                        //     textProp.Value = paramValue;
                        // }
                    }
                }

                // var tempContainer = refElement.OwnerDocument.CreateElement("temp");
                // for (int i = 0; i < macro.Body.Count; i++)
                // {
                //     var node = macro.Body[i];
                //     tempContainer.AppendChild(node.CloneNode(true));
                // }
                
                // macro.EvalAll(tempContainer);
                

                // for (int i = 0; i < tempContainer.ChildNodes.Count; i++)
                // {
                //     var node = tempContainer.ChildNodes[i];
                //     refElement.ParentNode?.InsertBefore(node, refElement);
                // }

                // refElement.ParentNode?.RemoveChild(refElement);
                // tempContainer.ParentNode?.RemoveChild(tempContainer);

                // replace ref element with macro instance
                var macroInstance = macro.GetMacroInstance(paramInputs);
                foreach (var node in macroInstance)
                {
                    refElement.ParentNode?.InsertBefore(node, refElement);
                }
                refElement.ParentNode?.RemoveChild(refElement);
            }
        }

    }

    public List<XmlNode> GetMacroInstance(Dictionary<string, IXacroProperty> paramProperties)
    {
        // duplicate macro el and evaluate all properties
        var macroCopy = (XmlElement)MacroElement.CloneNode(true);

        foreach (var prop in paramProperties)
        {
            if (Params.ContainsKey(prop.Key))
            {
                Params[prop.Key].Value = prop.Value.Value;
            }
            else Params[prop.Key] = prop.Value;
        }

        EvalAll(macroCopy);

        return macroCopy.ChildNodes.Cast<XmlNode>().ToList();
    }
}
