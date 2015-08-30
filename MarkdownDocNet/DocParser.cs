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
        Event
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
            foreach(var type in types)
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

            // Print the type name heading
            output.AppendLine("## " + type.Name);

            var typeType = "";

            if (type.IsValueType)
                typeType = "struct";
            else if (type.IsInterface)
                typeType = "interface";
            else if (type.IsClass)
                typeType = "class";

            // Print detailed declaration info
            if(type.BaseType == typeof(object))
                output.AppendLine(String.Format("*" + typeType + " " + type.FullName + "*"));
            else
                output.AppendLine(String.Format("*" + typeType + " " + type.FullName + ": " + type.BaseType.FullName + "*"));

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
            output.AppendLine("## " + type.Name);

            output.AppendLine(String.Format("*enum " + type.FullName + "*"));

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
            foreach (var property in members)
            {
                output.AppendLine(MemberListItem(property));
            }
            output.AppendLine("");

            return output.ToString();
        }

        public string MakeSignature(MethodInfo method, bool humanReadable=true)
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

        public string CSharpName(Type type)
        {
            var name = type.Name;

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

            var xSummary = member.Element("summary");
            if (xSummary != null)
                memberInfo.Summary = ParseDocText(xSummary);

            var xRemarks = member.Element("remarks");
            if (xRemarks != null)
                memberInfo.Remarks = ParseDocText(xRemarks);

            var xReturns = member.Element("returns");
            if (xReturns != null)
                memberInfo.Returns = ParseDocText(xReturns);

            var xExample = member.Element("example");
            if (xExample != null)
                memberInfo.Example = ParseDocText(xExample);

            var xParams = member.Elements("param");
            foreach(var param in xParams)
            {
                var name = param.Attribute("name").Value;
                memberInfo.ParameterDescriptionsByName[name] = ParseDocText(param);
            }

            return memberInfo;
        }

        public string ParseDocText(XNode node)
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
                    return LinkFromDescriptor(descriptor);
                }
                else if (element.Name == "code")
                {
                    var code = FixValueIndentation(element, element.Value);
                    ParseDocText(element.FirstNode);
                    return "\n```csharp\n" + code + "\n```\n";
                }
                else
                {
                    var output = new StringBuilder();
                    foreach (var child in element.Nodes())
                    {
                        if(child.NodeType == XmlNodeType.Element || child.NodeType == XmlNodeType.Text)
                            output.Append(ParseDocText(child));
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

        public string LinkFromDescriptor(string descriptor)
        {
            var link = FullNameFromDescriptor(descriptor);
            return " [" + link + "](#" + link + ") ";
        }

        public string FullNameFromDescriptor(string descriptor)
        {
            var descriptorElements = descriptor.Split(':');

            if (descriptorElements.Length != 2)
                throw new InvalidOperationException(String.Format("Invalid name descriptor: '{0}'", descriptor));

            return descriptorElements[1];
        }
    }
}
