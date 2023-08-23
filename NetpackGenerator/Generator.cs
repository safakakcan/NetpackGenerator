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

        public static void Generate(params Type[] types)
        {
            var codeNamespace = new CodeNamespace("Netpack");
            codeNamespace.Imports.Add(new CodeNamespaceImport("System"));
            codeNamespace.Imports.Add(new CodeNamespaceImport("System.Runtime.InteropServices"));
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
                    serializationMethod.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference(typeof(Span<byte>)), "Data"));
                    serializationMethod.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference(typeof(int)), "Index") { Direction = FieldDirection.Ref });

                    serializationMethod.Statements.Add(new CodeVariableDeclarationStatement(typeof(ushort), "ArraySize"));
                    GenerateFieldSerializer(serializationMethod.Statements, fields, typeName);

                    generatedClass.Members.Add(serializationMethod);
                }

                {   // Deserialization
                    CodeMemberMethod deserializationMethod = new CodeMemberMethod()
                    {
                        Name = "Deserialize",
                        Attributes = MemberAttributes.Public | MemberAttributes.Static
                    };
                    deserializationMethod.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference($"this Span<byte>"), "Data"));
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
                    var methodRefExpression = new CodeMethodReferenceExpression(new CodeSnippetExpression("MemoryMarshal"), "Write", new CodeTypeReference(field.FieldType));
                    var spanSliceExpression = new CodeMethodReferenceExpression(new CodeVariableReferenceExpression("Data"), "Slice");
                    statements.Add(new CodeCommentStatement($"Write value of {fieldName}"));
                    statements.Add(new CodeExpressionStatement(new CodeMethodInvokeExpression(methodRefExpression, new CodeMethodInvokeExpression(spanSliceExpression, new CodeVariableReferenceExpression("Index")), new CodeSnippetExpression($"ref {fieldName}"))));
                    statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("Index"), new CodeSnippetExpression($"Index + sizeof({field.FieldType.Name})")));
                    continue;
                }

                if (field.FieldType.IsArray || field.FieldType == typeof(string))
                {
                    var elementType = field.FieldType.GetElementType();

                    var iterationIndexName = string.Empty;
                    var iterationDepth = fieldName.Split('.').Length - 1;
                    for (int i = 0; i < iterationDepth; i++)
                    {
                        iterationIndexName += "i";
                    }

                    var sizeExpression = new CodeMethodReferenceExpression(new CodeSnippetExpression("MemoryMarshal"), "Write", new CodeTypeReference(typeof(ushort)));
                    var sizeSliceExpression = new CodeMethodReferenceExpression(new CodeVariableReferenceExpression("Data"), "Slice");
                    statements.Add(new CodeCommentStatement($"Write array size of {fieldName}"));
                    statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("ArraySize"), new CodeSnippetExpression($"(ushort){fieldName}.Length")));
                    statements.Add(new CodeExpressionStatement(new CodeMethodInvokeExpression(sizeExpression, new CodeMethodInvokeExpression(sizeSliceExpression, new CodeVariableReferenceExpression("Index")), new CodeSnippetExpression($"ref ArraySize"))));
                    statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("Index"), new CodeSnippetExpression($"Index + sizeof(ushort)")));

                    var iterationStatements = new CodeStatementCollection();

                    if (elementType.IsPrimitive && elementType != typeof(string))
                    {
                        var methodRefExpression = new CodeMethodReferenceExpression(new CodeSnippetExpression("MemoryMarshal"), "Write", new CodeTypeReference(elementType));
                        var spanSliceExpression = new CodeMethodReferenceExpression(new CodeVariableReferenceExpression("Data"), "Slice");

                        iterationStatements = new CodeStatementCollection()
                        {
                            new CodeExpressionStatement(new CodeMethodInvokeExpression(methodRefExpression, new CodeMethodInvokeExpression(spanSliceExpression, new CodeVariableReferenceExpression("Index")), new CodeSnippetExpression($"ref {fieldName}[{iterationIndexName}]"))),
                            new CodeAssignStatement(new CodeVariableReferenceExpression("Index"), new CodeSnippetExpression($"Index + sizeof({elementType.Name})"))
                        };
                    }
                    else
                    {
                        GenerateFieldSerializer(iterationStatements, elementType.GetFields(), $"{fieldName}[{iterationIndexName}]");
                    }

                    var iterationStatementArray = new CodeStatement[iterationStatements.Count];
                    for (int i = 0; i < iterationStatements.Count; i++)
                    {
                        iterationStatementArray[i] = iterationStatements[i];
                    }

                    statements.Add(new CodeCommentStatement($"Iterate {fieldName} array"));
                    statements.Add(new CodeIterationStatement(new CodeVariableDeclarationStatement(typeof(int), iterationIndexName, new CodeSnippetExpression("0")), new CodeSnippetExpression($"{iterationIndexName} < {fieldName}.Length"), new CodeSnippetStatement($"{iterationIndexName}++"), iterationStatementArray));
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
                    var methodRefExpression = new CodeMethodReferenceExpression(new CodeSnippetExpression("MemoryMarshal"), "Read", new CodeTypeReference(field.FieldType));
                    var spanSliceExpression = new CodeMethodReferenceExpression(new CodeVariableReferenceExpression("Data"), "Slice");
                    statements.Add(new CodeCommentStatement($"Read value of {fieldName}"));
                    statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression(fieldName), new CodeMethodInvokeExpression(methodRefExpression, new CodeMethodInvokeExpression(spanSliceExpression, new CodeVariableReferenceExpression("Index"), new CodeSnippetExpression($"sizeof({field.FieldType.Name})")))));
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