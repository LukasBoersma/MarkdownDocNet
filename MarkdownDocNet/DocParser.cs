using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace MarkdownDocNet
{

    public enum MemberType
    {
        Namespace,
        Type,
        Method,
        Field,
        Property,
        Event,
        Constructor
    }

    public class MemberDoc
    {
        public static MemberType TypeFromDescriptor(char descriptor)
        {
            switch(descriptor)
            {
                case 'T':
                    return MemberType.Type;
                case 'M':
                    return MemberType.Method;
                case 'E':
                    return MemberType.Event;
                case 'F':
                    return MemberType.Field;
                case 'P':
                    return MemberType.Property;
                default:
                    throw new ArgumentException("Unknown member descriptor: " + descriptor);
            }
        }

        public MemberType Type;
        public string FullName;
        public string LocalName;
        public string ParentName;

        public int Importance = 0;

        public string Summary;
        public string Remarks;
        public string Returns;
        public string Example;
        public Dictionary<string, string> ParameterDescriptionsByName = new Dictionary<string, string>();
    }

    public class DocParser
    {
        XDocument Doc;
        Assembly AssemblyInfo;
        string OutputFile;

        Dictionary<string, MemberDoc> MemberDocumentations = new Dictionary<string, MemberDoc>();

        public DocParser(string docFile, string assemblyFile, string outputFile)
        {
            Doc = XDocument.Load(docFile, LoadOptions.SetLineInfo);
            AssemblyInfo = Assembly.LoadFile(assemblyFile);
            OutputFile = outputFile;
        }

        public void ParseXml()
        {
            var members = Doc.Element("doc").Element("members").Elements("member");
            foreach (var member in members)
            {
                var memberInfo = ParseMember(member);
                MemberDocumentations[memberInfo.FullName] = memberInfo;
            }
        }

        public void GenerateDoc()
        {
            var output = new StringBuilder();

            var types = AssemblyInfo.GetExportedTypes();

            var typesSorted = types.OrderByDescending((Type a) =>
            {
                var impA = 0;
                if (MemberDocumentations.ContainsKey(a.FullName))
                    impA = MemberDocumentations[a.FullName].Importance;
                return impA;
            });

            foreach (var type in typesSorted)
            {
                var md = TypeToMarkdown(type);
                if (!String.IsNullOrEmpty(md))
                {
                    output.Append(md);
                    output.AppendLine("");
                    output.AppendLine("---");
                    output.AppendLine("");
                }
            }

            File.WriteAllText(OutputFile, output.ToString());
        }

        public string TypeToMarkdown(Type type)
        {
            if (type.BaseType == typeof(System.MulticastDelegate))
            {
                // Todo: Docs for delegate types
            }
            else if (type.IsEnum)
                return EnumToMarkdown(type);
            else if (type.IsClass || type.IsInterface)
                return ClassToMarkdown(type);

            return "";
        }

        public string ClassToMarkdown(Type type)
        {
            var output = new StringBuilder();

            // Only print members that are documented
            if (!MemberDocumentations.ContainsKey(type.FullName))
                return "";

            var doc = MemberDocumentations[type.FullName];

            output.AppendLine("<a id=\"" + type.FullName + "\"></a>");

            var typeType = "";

            if (type.IsValueType)
                typeType = "struct";
            else if (type.IsInterface)
                typeType = "interface";
            else if (type.IsClass)
                typeType = "class";
            
            // Print the type name heading
            output.AppendLine("## " + typeType + " " + type.FullName);

            if (type.BaseType != typeof(object))
                output.AppendLine("*Extends " + type.BaseType.FullName + "*");

            output.AppendLine("");

            // Print summary and remarks
            if (!String.IsNullOrEmpty(doc.Summary))
            {
                output.AppendLine(doc.Summary);
                output.AppendLine("");
            }

            if (!String.IsNullOrEmpty(doc.Remarks))
            {
                output.AppendLine(doc.Remarks);
                output.AppendLine("");
            }

            if (!String.IsNullOrEmpty(doc.Example))
            {
                output.AppendLine("**Examples**");
                output.AppendLine("");
                output.AppendLine(doc.Example);
                output.AppendLine("");
            }

            // Print overview of all members

            var memberOutput = new StringBuilder();

            var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (constructors.Length > 0)
            {
                output.Append(MemberListCategory("Constructors", constructors));
            }

            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (methods.Length > 0)
            {
                var methodList = new StringBuilder();
                bool foundRealMethods = false;

                methodList.AppendLine("**Methods**");
                methodList.AppendLine("");

                foreach (var method in methods)
                {
                    if (!method.IsConstructor && !method.IsSpecialName)
                    {
                        foundRealMethods = true;
                        methodList.Append(MemberListItem(method));
                    }
                }

                if (foundRealMethods)
                {
                    output.Append(methodList);
                    output.AppendLine("");
                }
            }

            var events = type.GetEvents(BindingFlags.Instance | BindingFlags.Public);
            if (events.Length > 0)
            {
                output.Append(MemberListCategory("Events", events));
            }

            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (properties.Length > 0 || fields.Length > 0)
            {
                output.Append(MemberListCategory("Properties and Fields", properties));
                output.Append(MemberListCategory(null, fields));
            }

            var staticMethods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (staticMethods.Length > 0)
            {
                var methodList = new StringBuilder();
                bool foundRealMethods = false;

                methodList.AppendLine("**Static Methods**");
                methodList.AppendLine("");

                foreach (var method in staticMethods)
                {
                    if (!method.IsConstructor && !method.IsSpecialName)
                    {
                        foundRealMethods = true;
                        methodList.Append(MemberListItem(method));
                    }
                }

                if (foundRealMethods)
                {
                    output.Append(methodList);
                    output.AppendLine("");
                }
            }

            var staticProperties = type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);
            var staticFields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);

            if (staticProperties.Length > 0)
            {
                output.Append(MemberListCategory("Static Properties and Fields", staticProperties));
                output.Append(MemberListCategory(null, staticFields));
            }

            output.AppendLine("");

            output.Append(memberOutput);

            return output.ToString();
        }

        public string EnumToMarkdown(Type type)
        {
            var output = new StringBuilder();

            // Only print members that are documented
            if (!MemberDocumentations.ContainsKey(type.FullName))
                return "";

            var doc = MemberDocumentations[type.FullName];

            // Print the type name heading
            output.AppendLine("<a id=\"" + type.FullName + "\"></a>");
            output.AppendLine("## enum " + type.FullName);

            output.AppendLine("");

            // Print summary and remarks
            if (!String.IsNullOrEmpty(doc.Summary))
                output.AppendLine(doc.Summary);

            if (!String.IsNullOrEmpty(doc.Remarks))
                output.AppendLine(doc.Remarks);

            output.AppendLine("");

            var values = type.GetEnumValues();

            if (values.Length > 0)
            {
                output.AppendLine("**Enum Values**");
                output.AppendLine("");
                foreach (var value in values)
                {
                    var name = type.GetEnumName(value);
                    output.AppendLine("* **" + name + "**");
                }
            }

            output.AppendLine("");

            return output.ToString();
        }

        public string MethodToMarkdown(MethodInfo method)
        {
            var output = new StringBuilder();

            var fullName = FullNameFromMember(method);

            // Only print members that are documented
            if (!MemberDocumentations.ContainsKey(fullName))
                return "";

            var doc = MemberDocumentations[fullName];
            
            output.AppendLine("<a id=\"" + fullName + "\"></a>");

            // Print the type name heading
            if (method.ReturnType == null || method.ReturnType == typeof(void))
                output.AppendLine("#### void " + method.Name + MakeSignature(method));
            else
                output.AppendLine("#### " + CSharpName(method.ReturnType) + " " + method.Name + MakeSignature(method));

            // Print summary and remarks
            if (!String.IsNullOrEmpty(doc.Summary))
                output.AppendLine(doc.Summary);

            if (!String.IsNullOrEmpty(doc.Remarks))
                output.AppendLine(doc.Remarks);

            output.AppendLine("");

            if (!String.IsNullOrEmpty(doc.Returns))
                output.AppendLine("**Returns:** " + doc.Returns);

            output.AppendLine("");

            var parameters = method.GetParameters();
            if (parameters.Length > 0)
            {
                output.AppendLine("**Parameters:**");
                foreach (var paramInfo in parameters)
                {
                    output.Append("* *" + CSharpName(paramInfo.ParameterType) + "* **" + paramInfo.Name + "**");
                    if (paramInfo.IsOptional)
                        output.Append(" *(optional, default: " + paramInfo.DefaultValue.ToString() + ")*");

                    output.AppendLine("");
                    output.AppendLine("");

                    if (doc.ParameterDescriptionsByName.ContainsKey(paramInfo.Name))
                    {
                        output.AppendLine("  " + doc.ParameterDescriptionsByName[paramInfo.Name]);
                        output.AppendLine("");
                    }
                }
            }

            output.AppendLine("");

            return output.ToString();
        }

        public string MemberListItem(MemberInfo member)
        {
            var fullName = FullNameFromMember(member);

            var output = new StringBuilder();

            output.AppendLine("<a id=\"" + FullNameFromMember(member) + "\"></a>");
            output.AppendLine("");
            output.Append("* ");

            if (member is MethodInfo)
            {
                var method = (MethodInfo)member;
                if (method.ReturnType == null || method.ReturnType == typeof(void))
                    output.Append("*void* ");
                else
                    output.Append("*" + CSharpName(method.ReturnType) + "* ");

                output.Append("**" + method.Name + "** *" + MakeSignature(method) + "*");
            }
            else if (member is ConstructorInfo)
            {
                var constructor = (ConstructorInfo)member;
                fullName = constructor.DeclaringType.FullName + ".#ctor" + MakeSignature(constructor, false);
                output.Append("**" + member.DeclaringType.Name + "** *" + MakeSignature(constructor) + "*");
            }
            else
            {
                Type type = null;
                if (member is FieldInfo)
                    type = ((FieldInfo)member).FieldType;
                else if (member is PropertyInfo)
                    type = ((PropertyInfo)member).PropertyType;

                if(type != null)
                    output.Append("*" + CSharpName(type) + "* ");

                output.Append("**" + member.Name + "**");
            }

            output.AppendLine("  ");

            
            if (MemberDocumentations.ContainsKey(fullName))
            {
                var doc = MemberDocumentations[fullName];
                if (!String.IsNullOrEmpty(doc.Summary))
                {
                    output.AppendLine("  " + doc.Summary + "  ");
                }
                if (!String.IsNullOrEmpty(doc.Remarks))
                {
                    output.AppendLine("  " + doc.Remarks);
                }
            }

            output.AppendLine("");

            return output.ToString();
        }

        public string MemberListCategory(string title, IEnumerable<MemberInfo> members)
        {
            var output = new StringBuilder();

            if (!String.IsNullOrEmpty(title))
            {
                output.AppendLine("**" + title + "**");
                output.AppendLine("");
            }
            foreach (var member in members)
            {
                output.AppendLine(MemberListItem(member));
            }
            output.AppendLine("");

            return output.ToString();
        }

        public string MakeSignature(MethodBase method, bool humanReadable=true)
        {
            var output = new StringBuilder();
            output.Append("(");
            var parameters = method.GetParameters();
            bool first = true;
            foreach(var p in parameters)
            {
                if(!first)
                    output.Append(humanReadable ? ", " : ",");

                if (p.IsOptional && humanReadable)
                    output.Append("[");


                if (humanReadable)
                {
                    output.Append(CSharpName(p.ParameterType));
                    output.Append(" ");
                    output.Append(p.Name);
                }
                else
                {
                    output.Append(p.ParameterType.FullName);
                }

                if (p.IsOptional && humanReadable)
                    output.Append("]");
                first = false;
            }
            output.Append(")");

            return output.ToString();
        }

        /// <summary>
        /// Returns the full name of the given member, in the same notation that is used in the XML documentation files for member ids and references.
        /// </summary>
        public string FullNameFromMember(MemberInfo member)
        {
            if (member is MethodInfo)
            {
                var method = (MethodInfo)member;
                if (method.GetParameters().Length > 0)
                    return method.DeclaringType.FullName + "." + method.Name + MakeSignature(method, humanReadable: false);
                else
                    return method.DeclaringType.FullName + "." + method.Name;
            }
            else
            {
                return member.DeclaringType.FullName + "." + member.Name;
            }
        }

        static Dictionary<Type, string> primitiveNames = new Dictionary<Type, string>
        {
            {typeof(byte), "byte"},
            {typeof(sbyte), "sbyte"},
            {typeof(short), "short"},
            {typeof(ushort), "ushort"},
            {typeof(int), "int"},
            {typeof(uint), "uint"},
            {typeof(long), "long"},
            {typeof(ulong), "ulong"},
            {typeof(char), "char"},
            {typeof(float), "float"},
            {typeof(double), "double"},
            {typeof(decimal), "decimal"},
            {typeof(bool), "bool"},
            {typeof(object), "object"},
            {typeof(string), "string"},
        };

        static HashSet<string> ignoredNamespaces = new HashSet<string>
        {
            "System",
            "System.Collections.Generic",
            "System.Text",
            "System.IO"
        };

        /// <summary>
        /// Returns the name of the given type as it would be notated in C#
        /// </summary>
        public string CSharpName(Type type)
        {
            var name = "";

            if (ignoredNamespaces.Contains(type.Namespace))
                name = type.Name;
            else
                name = type.FullName;

            name = name.Replace('+', '.');

            if ((type.IsPrimitive || type == typeof(string)) && primitiveNames.ContainsKey(type))
                return primitiveNames[type];

            if (!type.IsGenericType)
                return name;

            var output = new StringBuilder();
            output.Append(name.Substring(0, name.IndexOf('`')));
            output.Append("&lt;");
            output.Append(string.Join(", ", type.GetGenericArguments()
                                            .Select(t => CSharpName(t))));
            output.Append("&gt;");
            return output.ToString();
        }

        /// <summary>
        /// Parsed a single member node from the xml documentation and returns the corresponding MemberDoc object.
        /// </summary>
        public MemberDoc ParseMember(XElement member)
        {
            var memberInfo = new MemberDoc();

            string nameDescriptor = member.Attribute("name").Value;
            var descriptorElements = nameDescriptor.Split(':');

            if (descriptorElements.Length != 2)
                throw new InvalidOperationException(String.Format(
                        "Invalid name descriptor in line {0}: '{1}'",
                        ((IXmlLineInfo)member).LineNumber,
                        nameDescriptor
                        ));

            memberInfo.Type = MemberDoc.TypeFromDescriptor(descriptorElements[0][0]);

            memberInfo.FullName = descriptorElements[1];
            memberInfo.LocalName = memberInfo.FullName;

            var xImportance = member.Element("importance");
            if (xImportance != null)
            {
                int importance = 0;
                if(int.TryParse(xImportance.Value, out importance))
                    memberInfo.Importance = importance;
            }


            var xSummary = member.Element("summary");
            if (xSummary != null)
                memberInfo.Summary = ParseDocText(xSummary, memberInfo.FullName);

            var xRemarks = member.Element("remarks");
            if (xRemarks != null)
                memberInfo.Remarks = ParseDocText(xRemarks, memberInfo.FullName);

            var xReturns = member.Element("returns");
            if (xReturns != null)
                memberInfo.Returns = ParseDocText(xReturns, memberInfo.FullName);

            var xExample = member.Element("example");
            if (xExample != null)
                memberInfo.Example = ParseDocText(xExample, memberInfo.FullName);

            var xParams = member.Elements("param");
            foreach(var param in xParams)
            {
                var name = param.Attribute("name").Value;
                memberInfo.ParameterDescriptionsByName[name] = ParseDocText(param, memberInfo.FullName);
            }

            return memberInfo;
        }

        /// <summary>
        /// Parses the text inside a given XML node and returns a Markdown version of it.
        /// </summary>
        public string ParseDocText(XNode node, string contextMemberName)
        {
            if (node.NodeType == XmlNodeType.Text)
            {
                var text = ((XText)node).Value;
                return FixValueIndentation(node, text);
            }
            else if (node.NodeType == XmlNodeType.Element)
            {
                var element = (XElement)node;
                if (element.Name == "see")
                {
                    var descriptor = element.Attribute("cref").Value;
                    string linkName = null;
                    if (!String.IsNullOrEmpty(element.Value))
                        linkName = element.Value;
                    return LinkFromDescriptor(descriptor, contextMemberName, linkName);
                }
                else if (element.Name == "code")
                {
                    var code = FixValueIndentation(element, element.Value);
                    ParseDocText(element.FirstNode, contextMemberName);
                    return "\n```csharp\n" + code + "\n```\n";
                }
                else
                {
                    var output = new StringBuilder();
                    foreach (var child in element.Nodes())
                    {
                        if(child.NodeType == XmlNodeType.Element || child.NodeType == XmlNodeType.Text)
                            output.Append(ParseDocText(child, contextMemberName));
                    }
                    return output.ToString();
                }
            }
            else
                return "";
        }

        public string FixValueIndentation(XNode node, string text)
        {
            var lineInfo = (IXmlLineInfo)node.Parent;
            var indentationCount = lineInfo.LinePosition - 2;
            if (indentationCount > 0)
            {
                var indentation = "\n" + new String(' ', indentationCount);
                text = text.Replace(indentation, "\n");
            }

            return text.Trim();
        }

        public string LinkFromDescriptor(string descriptor, string contextMemberName, string linkName = null)
        {
            var link = FullNameFromDescriptor(descriptor);

            if(linkName == null)
                return " [" + HumanNameFromDescriptor(descriptor, contextMemberName) + "](#" + link + ") ";
            else
                return " [" + linkName + "](#" + link + ") ";
        }

        public string FullNameFromDescriptor(string descriptor)
        {
            var descriptorElements = descriptor.Split(':');

            if (descriptorElements.Length != 2)
                throw new InvalidOperationException(String.Format("Invalid name descriptor: '{0}'", descriptor));

            return descriptorElements[1];
        }

        public string HumanNameFromDescriptor(string descriptor, string parentTypeOrNamespace = null)
        {
            var descriptorElements = descriptor.Split(':');

            if (descriptorElements.Length != 2 || descriptorElements[0].Length != 1)
                throw new InvalidOperationException(String.Format("Invalid name descriptor: '{0}'", descriptor));

            var memberType = MemberDoc.TypeFromDescriptor(descriptorElements[0][0]);
            var fullName = descriptorElements[1];

            // Cut away any method signatures
            var fullNameNoSig = fullName.Split(new char[] { '(' }, 2)[0];


            if (String.IsNullOrEmpty(parentTypeOrNamespace))
                return fullName;

            var commonPrefix = "";
            var dotIndex = fullNameNoSig.LastIndexOf('.');
            if (dotIndex >= 0)
            {
                var possiblePrefix = fullNameNoSig.Substring(0, dotIndex);
                commonPrefix = CommonNamespacePrefix(possiblePrefix, parentTypeOrNamespace);
            }

            //if(memberType == MemberType.Type || memberType == MemberType.Namespace)
                return fullNameNoSig.Substring(commonPrefix.Length + 1);
            /*else
            {
                var declaringTypeName = fullNameNoSig.Substring(0, fullNameNoSig.LastIndexOf('.'));
                var declaringType = AssemblyInfo.GetType(declaringTypeName);
                if(declaringType == null || declaringType.FullName != parentTypeOrNamespace)
                    return fullNameNoSig.Substring(commonPrefix.Length + 1);
                else
                {
                    // So the given descriptor 
                    var memberName = fullName.Substring(commonPrefix.Length + 1);
                    

                    // For everything except methods, just return the member name
                    if (memberType != MemberType.Method)
                        return memberName;

                    // Try to find the exact matching method so we can print the correct signature
                    var possibleMatches = declaringType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);
                    foreach(var match in possibleMatches)
                    {
                        var memberId = FullNameFromMember(match);
                        if (memberId == fullName)
                            return match.Name + MakeSignature(match, true);
                    }

                    return memberName;
                }
            }
            */
        }

        public static string CommonNamespacePrefix(string fullName1, string fullName2)
        {
            var elements1 = fullName1.Split('.');
            var elements2 = fullName2.Split('.');

            var potentialMatchLength = Math.Min(elements1.Length, elements2.Length);

            var output = new StringBuilder();
            bool first = true;

            for (var i = 0; i < potentialMatchLength; i++)
            {
                if (elements1[i].Equals(elements2[i]))
                {
                    if (!first)
                        output.Append(".");
                    first = false;

                    output.Append(elements1[i]);
                }
                else
                    break;
            }

            return output.ToString();
        }
    }
}
