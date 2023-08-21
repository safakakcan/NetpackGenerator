using Microsoft.CSharp;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;

namespace Netpack
{
    public static class Generator
    {
        private const string GeneratedClassName = "Serializer";
        private const string writerBody = "using (MemoryStream ms = new MemoryStream())\r\n            {\r\n                using (BinaryWriter writer = new BinaryWriter(ms))\r\n                {\r\n                    {0}\r\n                }\r\n\r\n                bytes = ms.ToArray();\r\n            }";

        public static void Generate(params Type[] types)
        {
            var codeNamespace = new CodeNamespace("Netpack");
            codeNamespace.Imports.Add(new CodeNamespaceImport("System"));
            var generatedClass = new CodeTypeDeclaration(GeneratedClassName);
            codeNamespace.Types.Add(generatedClass);
            generatedClass.TypeAttributes = TypeAttributes.Public;
            generatedClass.Attributes = MemberAttributes.Public | MemberAttributes.Static;
            generatedClass.IsClass = true;

            foreach (var type in types)
            {
                var fields = type.GetFields();
                var typeName = type.Name;

                {   // Serialization
                    CodeMemberMethod serializationMethod = new CodeMemberMethod()
                    {
                        Name = "Serialize",
                        Attributes = MemberAttributes.Public | MemberAttributes.Static
                    };
                    serializationMethod.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference($"this {type.Name}"), typeName));
                    serializationMethod.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference(typeof(byte[])), "Data") { Direction = FieldDirection.Ref });
                    serializationMethod.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference(typeof(int)), "Index") { Direction = FieldDirection.Ref });

                    serializationMethod.Statements.Add(new CodeVariableDeclarationStatement(typeof(byte[]), "Bytes", new CodeArrayCreateExpression(typeof(byte[]), 0)));
                    GenerateFieldSerializer(serializationMethod.Statements, fields, typeName);

                    generatedClass.Members.Add(serializationMethod);
                }

                {   // Deserialization
                    CodeMemberMethod deserializationMethod = new CodeMemberMethod()
                    {
                        Name = "Deserialize",
                        Attributes = MemberAttributes.Public | MemberAttributes.Static
                    };
                    deserializationMethod.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference($"this byte[]"), "Data"));
                    deserializationMethod.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference(typeof(int)), "Index") { Direction = FieldDirection.Ref });
                    deserializationMethod.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference(type), typeName) { Direction = FieldDirection.Out });

                    GenerateFieldDeserializer(deserializationMethod.Statements, fields, type, typeName);

                    generatedClass.Members.Add(deserializationMethod);
                }
            }

            CodeDomProvider provider = new CSharpCodeProvider();
            using (StringWriter writer = new StringWriter())
            {
                provider.GenerateCodeFromNamespace(codeNamespace, writer, new CodeGeneratorOptions());
                writer.ToString();
            }

            var generatedFile = Path.Combine(Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName, "Generateds.cs");
            var stringBuilder = new StringBuilder();

            using (StringWriter writer = new StringWriter(stringBuilder))
            {
                provider.GenerateCodeFromNamespace(codeNamespace, writer, new CodeGeneratorOptions());
                var newCode = stringBuilder.ToString().Replace($"public class {GeneratedClassName}", $"public static class {GeneratedClassName}");
                File.WriteAllText(generatedFile, newCode);
            }
        }

        private static void GenerateFieldSerializer(CodeStatementCollection statements, FieldInfo[] fields, string parentName)
        {
            foreach (var field in fields)
            {
                var fieldName = string.IsNullOrEmpty(parentName) ? field.Name : $"{parentName}.{field.Name}";

                if (field.FieldType.IsPrimitive && field.FieldType != typeof(string))
                {
                    statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("Bytes"), new CodeMethodInvokeExpression(new CodeSnippetExpression("BitConverter"), "GetBytes", new CodeVariableReferenceExpression(fieldName))));
                    statements.Add(new CodeMethodInvokeExpression(new CodeSnippetExpression("Buffer"), "BlockCopy", new CodeVariableReferenceExpression("Bytes"), new CodeSnippetExpression("0"), new CodeSnippetExpression("Data"), new CodeSnippetExpression("Index"), new CodeSnippetExpression("Bytes.Length")));
                    statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("Index"), new CodeSnippetExpression("Index + Bytes.Length")));
                    continue;
                }

                if (field.FieldType.IsArray || field.FieldType == typeof(string))
                {
                    var elementType = field.FieldType.GetElementType();
                    var iterationStatements = new CodeStatement[]
                    {
                        new CodeAssignStatement(new CodeVariableReferenceExpression("Bytes"), new CodeMethodInvokeExpression(new CodeSnippetExpression("BitConverter"), "GetBytes", new CodeVariableReferenceExpression($"{fieldName}[i]"))),
                        new CodeExpressionStatement(new CodeMethodInvokeExpression(new CodeSnippetExpression("Buffer"), "BlockCopy", new CodeVariableReferenceExpression("Bytes"), new CodeSnippetExpression("0"), new CodeSnippetExpression("Data"), new CodeSnippetExpression("Index"), new CodeSnippetExpression("Bytes.Length"))),
                        new CodeAssignStatement(new CodeVariableReferenceExpression("Index"), new CodeSnippetExpression("Index + Bytes.Length"))
                    };
                    statements.Add(new CodeIterationStatement(new CodeVariableDeclarationStatement(typeof(int), "i", new CodeSnippetExpression("0")), new CodeSnippetExpression($"i < {fieldName}.Length"), new CodeSnippetStatement("i++"), iterationStatements));
                    continue;
                }
                
                {
                    GenerateFieldSerializer(statements, field.FieldType.GetFields(), fieldName);
                    continue;
                }
            }
        }

        private static void GenerateFieldDeserializer(CodeStatementCollection statements, FieldInfo[] fields, Type parentType, string parentName)
        {
            statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression(parentName), new CodeSnippetExpression($"new()")));

            foreach (var field in fields)
            {
                var fieldName = string.IsNullOrEmpty(parentName) ? field.Name : $"{parentName}.{field.Name}";

                if (field.FieldType.IsPrimitive && field.FieldType != typeof(string))
                {
                    statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression(fieldName), new CodeMethodInvokeExpression(new CodeSnippetExpression("BitConverter"), "To" + field.FieldType.Name, new CodeVariableReferenceExpression("Data"), new CodeVariableReferenceExpression("Index"))));
                    statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("Index"), new CodeSnippetExpression($"Index + sizeof({field.FieldType.Name})")));
                    continue;
                }

                if (field.FieldType.IsArray || field.FieldType == typeof(string))
                {

                    continue;
                }

                {
                    GenerateFieldDeserializer(statements, field.FieldType.GetFields(), field.FieldType, fieldName);
                    continue;
                }
            }
        }
    }
}