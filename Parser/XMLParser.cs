using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Threading.Tasks;
using System.Xml.Serialization;
using OpenGL_XML_Specification;
using System.Collections.Generic;

namespace GLThreadGen
{
    public class KhronosXMLParser
    {
        public KhronosXMLParser(Stream stream, GLDataRegistry registry)
        {
            if (!stream.CanRead)
            {
                throw new ArgumentException("Input stream must be readable");
            }

            Stream = stream;
			Registry = registry;
        }

		public GLDataRegistry Registry { get; private set; }
        public Registry XMLRegistry { get; private set; }

        private readonly static Dictionary<string, string> _HardcodedPascalReplacements = new Dictionary<string, string>{
            {"Srgb", "SRGB"},
            {"Rgba", "RGBA"},
            {"Rgb", "RGB"},
            {"Rg", "RG"},
            {"Bgra", "BGRA"},
            {"Bgr", "BGR"}
        };

        private readonly static Dictionary<string, string> _HardcodedEnumNameReplacements = new Dictionary<string, string>{
            {"BufferTargetARB", "BufferTarget"}
        };

        public string SCREAMING_SNAKE_ToPascalCase(string snake)
        {
            const int offset = 'a' - 'A';
            var build = new StringBuilder(snake.Length);
            bool cap = true;
            for(int i = 0; i < snake.Length; ++i)
            {
                char c = snake[i];
                if (c == '_')
                {
                    cap = true;
                    continue;
                }
                
                if (c < 'A' || c > 'Z') cap = true;
                build.Append((char)(c + (cap ? 0 : offset)));
                cap = false;

                if (LineParser.IsNumeric(c))
                {
                    cap = true;
                }
            }
            
            // This is terrible, but we don't need to be super efficient here.
            var val = build.ToString();
            foreach(var rep in _HardcodedPascalReplacements)
            {
                val = val.Replace(rep.Key, rep.Value);
            }
            return val;
        }

        private string GetTypedEnum(Registry xmlRegistry, string groupName)
        {
            var enums = Registry.EnumTypes;
            string enumName = null;
            if (!enums.ContainsKey(groupName))
            {
                foreach(var group in xmlRegistry.Groups.Group)
                {
                    if (group.Name == groupName)
                    {
                        var enumEntry = new GLEnum();
                        enumEntry.Name = group.Name;
                        foreach(var en in group.Enum)
                        {
                            if (Registry.Defines.Contains(en.Name))
                            {
                                // TODO: Extract enum value
                                enumEntry.Values.TryAdd(SCREAMING_SNAKE_ToPascalCase(en.Name.Substring("GL_".Length)), en.Name);
                            }
                        }

                        enums.Add(groupName, enumEntry);
                        if (_HardcodedEnumNameReplacements.ContainsKey(enumEntry.Name))
                        {
                            enumEntry.Name = _HardcodedEnumNameReplacements[enumEntry.Name];
                        }
                        enumName = enumEntry.Name;
                        break;
                    }
                }
            }
            else
            {
                enumName = enums[groupName].Name;
            }
            return enumName;
        }

        public Stream Stream { get; private set; }
		public void Parse()
		{
            var reader = new XmlSerializer(typeof(Registry));
            var xmlRegistry = (Registry)reader.Deserialize(Stream);
            if (xmlRegistry == null)
            {
                throw new XmlException("Failed to deserialize object");
            }
            XMLRegistry = xmlRegistry;

            var enums = Registry.EnumTypes;
            int numFound = 0;
            foreach(var command in xmlRegistry.Commands.Command)
            {
                FunctionEntry func;
                Registry.Functions.TryGetValue(command.Proto.Name, out func);
                if (func != null)
                {
                    ++numFound;
                    if (command.Proto.Ptype == "GLenum" && command.Proto.Group != null)
                    {
                        string enumName = GetTypedEnum(xmlRegistry, command.Proto.Group);
                        if (enumName != null)
                        {
                            func.Type.ReturnType = enumName;
                            func.Type.IsReturnEnum = true;
                        }
                    }

                    foreach(var param in command.Param)
                    {
                        if (param.Ptype == "GLenum" && param.Group != null)
                        {
                            string enumName = GetTypedEnum(xmlRegistry, param.Group);
                            if (enumName != null)
                            {
                                foreach(var fArg in func.Type.Arguments)
                                {
                                    if (fArg.Name == param.Name)
                                    {
                                        if (fArg.Type == "GLenum")
                                        {
                                            fArg.IsEnumType = true;
                                            fArg.Type = enumName;
                                        }
                                        else
                                        {
                                            // TODO: This doesn't work in the general case, so let's not bother with it
                                            //fArg.Type = fArg.Type.Replace("GLenum", "multigl::" + enumName);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            if (numFound != Registry.Functions.Count)
            {
                Console.WriteLine("Warning: Function count mismatch between XML and header.");
            }
            else
            {
                Console.WriteLine("All functions found");
            }
		}
	}
}
