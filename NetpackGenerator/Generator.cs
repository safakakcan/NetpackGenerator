﻿using Microsoft.CSharp;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Reflection;
using System.Text;

namespace Netpack
{
    public static class Generator
    {
        private const string GeneratedClassName = "NetpackSerializer";

        public static void Generate()
        {
            var codeNamespace = new CodeNamespace("Netpack");
            codeNamespace.Imports.Add(new CodeNamespaceImport("System"));
            codeNamespace.Imports.Add(new CodeNamespaceImport("System.Text"));
            codeNamespace.Imports.Add(new CodeNamespaceImport("System.Runtime.InteropServices"));
            var generatedClass = new CodeTypeDeclaration(GeneratedClassName);
            codeNamespace.Types.Add(generatedClass);
            generatedClass.TypeAttributes = TypeAttributes.Public;
            generatedClass.Attributes = MemberAttributes.Public | MemberAttributes.Static;
            generatedClass.IsClass = true;

            var types = FindAllSerializableTypes();
            if (!types.Any()) return;

            foreach (var type in types)
            {
                Console.WriteLine($"Generating Type: {type.Name}");
                var fields = type.GetFields();
                var typeName = type.Name;

                { // Serialization
                    CodeMemberMethod serializationMethod = new CodeMemberMethod()
                    {
                        Name = "Serialize",
                        Attributes = MemberAttributes.Private | MemberAttributes.Static
                    };
                    serializationMethod.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference($"this {type.Name}"), typeName));
                    serializationMethod.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference(typeof(Span<byte>)), "Data"));
                    serializationMethod.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference(typeof(int)), "Index")
                    {
                        Direction = FieldDirection.Ref
                    });
                    
                    CodeMemberMethod publicSerializationMethod = new CodeMemberMethod()
                    {
                        Name = "Serialize",
                        Attributes = MemberAttributes.Public | MemberAttributes.Static
                    };
                    publicSerializationMethod.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference($"this {type.Name}"), typeName));
                    publicSerializationMethod.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference(typeof(Span<byte>)), "Data"));
                    publicSerializationMethod.Statements.Add(new CodeVariableDeclarationStatement(typeof(int), "Index", new CodeSnippetExpression("0")));
                    publicSerializationMethod.Statements.Add(new CodeMethodInvokeExpression(null, "Serialize", new []
                    {
                        new CodeSnippetExpression(typeName), new CodeSnippetExpression("Data"), new CodeSnippetExpression("ref Index")
                    }));

                    serializationMethod.Statements.Add(new CodeVariableDeclarationStatement(typeof(ushort), "ArraySize"));
                    serializationMethod.Statements.Add(new CodeVariableDeclarationStatement(typeof(int), "ByteCount"));
                    GenerateFieldSerializer(serializationMethod.Statements, fields, typeName);

                    generatedClass.Members.Add(publicSerializationMethod);
                    generatedClass.Members.Add(serializationMethod);
                }

                { // Deserialization
                    CodeMemberMethod deserializationMethod = new CodeMemberMethod()
                    {
                        Name = "Deserialize",
                        Attributes = MemberAttributes.Private | MemberAttributes.Static
                    };
                    deserializationMethod.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference($"this Span<byte>"), "Data"));
                    deserializationMethod.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference(typeof(int)), "Index")
                    {
                        Direction = FieldDirection.Ref
                    });
                    deserializationMethod.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference(type), typeName)
                    {
                        Direction = FieldDirection.Ref
                    });
                    
                    CodeMemberMethod publicDeserializationMethod = new CodeMemberMethod()
                    {
                        Name = "Deserialize",
                        Attributes = MemberAttributes.Public | MemberAttributes.Static
                    };
                    publicDeserializationMethod.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference($"this Span<byte>"), "Data"));
                    publicDeserializationMethod.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference(type), typeName)
                    {
                        Direction = FieldDirection.Ref
                    });
                    publicDeserializationMethod.Statements.Add(new CodeVariableDeclarationStatement(typeof(int), "Index", new CodeSnippetExpression("0")));
                    publicDeserializationMethod.Statements.Add(new CodeMethodInvokeExpression(null, "Deserialize", new []
                    {
                        new CodeSnippetExpression("Data"), new CodeSnippetExpression("ref Index"), new CodeSnippetExpression($"ref {typeName}")
                    }));
                    
                    deserializationMethod.Statements.Add(new CodeVariableDeclarationStatement(typeof(ushort), "ArraySize"));
                    deserializationMethod.Statements.Add(new CodeVariableDeclarationStatement(typeof(int), "ByteCount"));
                    GenerateFieldDeserializer(deserializationMethod.Statements, fields, type, typeName, false);

                    generatedClass.Members.Add(publicDeserializationMethod);
                    generatedClass.Members.Add(deserializationMethod);
                }
            }

            CodeDomProvider provider = new CSharpCodeProvider();
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

                if (field.FieldType == typeof(string))
                {
                    WriteArraySize(statements, fieldName);

                    var byteCountVarExpression = new CodeAssignStatement(new CodeVariableReferenceExpression("ByteCount"),
                        new CodeMethodInvokeExpression(new CodeSnippetExpression("Encoding.UTF8"), "GetByteCount",
                            new CodeVariableReferenceExpression(fieldName)));
                    var targetSpan = new CodeMethodInvokeExpression(
                        new CodeMethodReferenceExpression(new CodeVariableReferenceExpression("Data"), "Slice"),
                        new CodeVariableReferenceExpression("Index"), new CodeVariableReferenceExpression("ByteCount"));
                    statements.Add(byteCountVarExpression);
                    statements.Add(new CodeExpressionStatement(new CodeMethodInvokeExpression(new CodeSnippetExpression("Encoding.UTF8"), "GetBytes",
                        new CodeVariableReferenceExpression(fieldName), targetSpan)));
                    statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("Index"),
                        new CodeSnippetExpression($"Index + ByteCount")));
                    continue;
                }

                if (field.FieldType.IsPrimitive)
                {
                    var methodRefExpression = new CodeMethodReferenceExpression(new CodeSnippetExpression("MemoryMarshal"), "Write",
                        new CodeTypeReference(field.FieldType));
                    var spanSliceExpression = new CodeMethodReferenceExpression(new CodeVariableReferenceExpression("Data"), "Slice");
                    statements.Add(new CodeCommentStatement($"Write value of {fieldName}"));
                    statements.Add(new CodeExpressionStatement(new CodeMethodInvokeExpression(methodRefExpression,
                        new CodeMethodInvokeExpression(spanSliceExpression, new CodeVariableReferenceExpression("Index")),
                        new CodeSnippetExpression($"ref {fieldName}"))));
                    statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("Index"),
                        new CodeSnippetExpression($"Index + sizeof({field.FieldType.Name})")));
                    continue;
                }

                if (field.FieldType.IsArray)
                {
                    var elementType = field.FieldType.GetElementType();

                    if (elementType.IsPrimitive)
                    {
                        WriteArraySize(statements, fieldName);
                        
                        var methodRefExpression = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(
                                new CodeSnippetExpression("MemoryMarshal"), "Cast",
                                new CodeTypeReference(elementType), new CodeTypeReference(typeof(byte))),
                            new CodeSnippetExpression($"{fieldName}.AsSpan()"));
                        var spanVariableExpression = new CodeVariableDeclarationStatement($"Span<byte>", $"{field.Name}Span", methodRefExpression);
                        var spanSliceExpression = new CodeMethodReferenceExpression(new CodeVariableReferenceExpression("Data"), "Slice");

                        statements.Add(spanVariableExpression);
                        statements.Add(new CodeExpressionStatement(new CodeMethodInvokeExpression(
                            new CodeVariableReferenceExpression($"{field.Name}Span"), "CopyTo",
                            new CodeMethodInvokeExpression(spanSliceExpression, new CodeVariableReferenceExpression("Index")))));
                        statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("Index"),
                            new CodeSnippetExpression($"Index + sizeof({elementType.Name}) * ArraySize")));
                    }
                    else
                    {
                        var iterationIndexName = string.Empty;
                        var iterationDepth = fieldName.Split('.').Length - 1;
                        for (int i = 0; i < iterationDepth; i++)
                        {
                            iterationIndexName += "i";
                        }

                        WriteArraySize(statements, fieldName);

                        var iterationStatements = new CodeStatementCollection();

                        if (elementType == typeof(string))
                        {
                            WriteArraySize(iterationStatements, $"{fieldName}[{iterationIndexName}]");

                            var byteCountVarExpression = new CodeAssignStatement(new CodeVariableReferenceExpression("ByteCount"),
                                new CodeMethodInvokeExpression(new CodeSnippetExpression("Encoding.UTF8"), "GetByteCount",
                                    new CodeVariableReferenceExpression($"{fieldName}[{iterationIndexName}]")));
                            var targetSpan = new CodeMethodInvokeExpression(
                                new CodeMethodReferenceExpression(new CodeVariableReferenceExpression("Data"), "Slice"),
                                new CodeVariableReferenceExpression("Index"), new CodeVariableReferenceExpression("ByteCount"));
                            iterationStatements.Add(byteCountVarExpression);
                            iterationStatements.Add(new CodeExpressionStatement(new CodeMethodInvokeExpression(
                                new CodeSnippetExpression("Encoding.UTF8"),
                                "GetBytes", new CodeSnippetExpression($"{fieldName}[{iterationIndexName}]"), targetSpan)));
                            iterationStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("Index"),
                                new CodeSnippetExpression($"Index + ByteCount")));
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
                        statements.Add(new CodeIterationStatement(
                            new CodeVariableDeclarationStatement(typeof(int), iterationIndexName, new CodeSnippetExpression("0")),
                            new CodeSnippetExpression($"{iterationIndexName} < {fieldName}.Length"),
                            new CodeSnippetStatement($"{iterationIndexName}++"),
                            iterationStatementArray));
                    }

                    continue;
                }

                {
                    GenerateFieldSerializer(statements, field.FieldType.GetFields(), fieldName);
                    continue;
                }
            }
        }

        private static void GenerateFieldDeserializer(CodeStatementCollection statements, FieldInfo[] fields, Type parentType, string parentName, bool initObject = true)
        {
            if (initObject) statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression(parentName), new CodeSnippetExpression($"new()")));

            foreach (var field in fields)
            {
                var fieldName = string.IsNullOrEmpty(parentName) ? field.Name : $"{parentName}.{field.Name}";

                if (field.FieldType == typeof(string))
                {
                    ReadArraySize(statements, fieldName);

                    var byteCountVarExpression = new CodeAssignStatement(new CodeVariableReferenceExpression("ByteCount"),
                        new CodeVariableReferenceExpression("ArraySize"));
                    var targetSpan = new CodeMethodInvokeExpression(
                        new CodeMethodReferenceExpression(new CodeVariableReferenceExpression("Data"), "Slice"),
                        new CodeVariableReferenceExpression("Index"), new CodeVariableReferenceExpression("ByteCount"));
                    statements.Add(byteCountVarExpression);
                    statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression(fieldName),
                        new CodeMethodInvokeExpression(new CodeSnippetExpression("Encoding.UTF8"), "GetString", targetSpan)));
                    statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("Index"),
                        new CodeSnippetExpression($"Index + ByteCount")));
                    continue;
                }

                if (field.FieldType.IsPrimitive)
                {
                    var methodRefExpression = new CodeMethodReferenceExpression(new CodeSnippetExpression("MemoryMarshal"), "Read",
                        new CodeTypeReference(field.FieldType));
                    var spanSliceExpression = new CodeMethodReferenceExpression(new CodeVariableReferenceExpression("Data"), "Slice");
                    statements.Add(new CodeCommentStatement($"Read value of {fieldName}"));
                    statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression(fieldName),
                        new CodeMethodInvokeExpression(methodRefExpression,
                            new CodeMethodInvokeExpression(spanSliceExpression, new CodeVariableReferenceExpression("Index"),
                                new CodeSnippetExpression($"sizeof({field.FieldType.Name})")))));
                    statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("Index"),
                        new CodeSnippetExpression($"Index + sizeof({field.FieldType.Name})")));
                    continue;
                }

                if (field.FieldType.IsArray)
                {
                    var elementType = field.FieldType.GetElementType();

                    if (elementType.IsPrimitive)
                    {
                        ReadArraySize(statements, fieldName);

                        var methodRefExpression = new CodeMethodReferenceExpression(new CodeSnippetExpression("MemoryMarshal"), "Cast",
                            new CodeTypeReference(typeof(byte)), new CodeTypeReference(elementType));
                        var spanSliceExpression = new CodeMethodReferenceExpression(new CodeVariableReferenceExpression("Data"), "Slice");

                        statements.Add(new CodeAssignStatement(
                            new CodeVariableReferenceExpression(fieldName),
                            new CodeMethodInvokeExpression(
                                new CodeMethodInvokeExpression(methodRefExpression,
                                    new CodeMethodInvokeExpression(spanSliceExpression,
                                        new CodeSnippetExpression("Index"), new CodeSnippetExpression($"sizeof({elementType}) * ArraySize"))),
                                "ToArray")));
                        statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("Index"),
                            new CodeSnippetExpression($"Index + sizeof({elementType.Name}) * ArraySize")));
                    }
                    else
                    {
                        var iterationIndexName = string.Empty;
                        var iterationDepth = fieldName.Split('.').Length - 1;
                        for (int i = 0; i < iterationDepth; i++)
                        {
                            iterationIndexName += "i";
                        }

                        ReadArraySize(statements, fieldName);

                        var iterationStatements = new CodeStatementCollection();

                        if (elementType == typeof(string))
                        {
                            statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression(fieldName),
                                new CodeSnippetExpression($"new {elementType.Name}[ArraySize]")));
                            ReadArraySize(iterationStatements, fieldName);

                            var byteCountVarExpression = new CodeAssignStatement(new CodeVariableReferenceExpression("ByteCount"),
                                new CodeVariableReferenceExpression("ArraySize"));
                            var targetSpan = new CodeMethodInvokeExpression(
                                new CodeMethodReferenceExpression(new CodeVariableReferenceExpression("Data"), "Slice"),
                                new CodeVariableReferenceExpression("Index"), new CodeVariableReferenceExpression("ByteCount"));
                            iterationStatements.Add(byteCountVarExpression);
                            iterationStatements.Add(new CodeAssignStatement(new CodeSnippetExpression($"{fieldName}[{iterationIndexName}]"),
                                new CodeMethodInvokeExpression(new CodeSnippetExpression("Encoding.UTF8"), "GetString", targetSpan)));
                            iterationStatements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("Index"),
                                new CodeSnippetExpression($"Index + ByteCount")));
                        }
                        else
                        {
                            statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression(fieldName),
                                new CodeSnippetExpression($"new {elementType.Name}[ArraySize]")));
                            GenerateFieldDeserializer(iterationStatements, elementType.GetFields(), elementType,
                                $"{fieldName}[{iterationIndexName}]");
                        }

                        var iterationStatementArray = new CodeStatement[iterationStatements.Count];
                        for (int i = 0; i < iterationStatements.Count; i++)
                        {
                            iterationStatementArray[i] = iterationStatements[i];
                        }

                        statements.Add(new CodeCommentStatement($"Iterate {fieldName} array"));
                        statements.Add(new CodeIterationStatement(
                            new CodeVariableDeclarationStatement(typeof(int), iterationIndexName, new CodeSnippetExpression("0")),
                            new CodeSnippetExpression($"{iterationIndexName} < {fieldName}.Length"),
                            new CodeSnippetStatement($"{iterationIndexName}++"),
                            iterationStatementArray));
                    }

                    continue;
                }

                {
                    GenerateFieldDeserializer(statements, field.FieldType.GetFields(), field.FieldType, fieldName);
                    continue;
                }
            }
        }

        private static void WriteArraySize(CodeStatementCollection statements, string fieldName)
        {
            var sizeExpression =
                new CodeMethodReferenceExpression(new CodeSnippetExpression("MemoryMarshal"), "Write", new CodeTypeReference(typeof(ushort)));
            var sizeSliceExpression = new CodeMethodReferenceExpression(new CodeVariableReferenceExpression("Data"), "Slice");
            statements.Add(new CodeCommentStatement($"Write array size of {fieldName}"));
            statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("ArraySize"),
                new CodeSnippetExpression($"(ushort){fieldName}.Length")));
            statements.Add(new CodeExpressionStatement(new CodeMethodInvokeExpression(sizeExpression,
                new CodeMethodInvokeExpression(sizeSliceExpression, new CodeVariableReferenceExpression("Index")),
                new CodeSnippetExpression($"ref ArraySize"))));
            statements.Add(
                new CodeAssignStatement(new CodeVariableReferenceExpression("Index"), new CodeSnippetExpression($"Index + sizeof(ushort)")));
        }

        private static void ReadArraySize(CodeStatementCollection statements, string fieldName)
        {
            var sizeExpression =
                new CodeMethodReferenceExpression(new CodeSnippetExpression("MemoryMarshal"), "Read", new CodeTypeReference(typeof(ushort)));
            var sizeSliceExpression = new CodeMethodReferenceExpression(new CodeVariableReferenceExpression("Data"), "Slice");
            var readSizeExpression = new CodeMethodInvokeExpression(sizeExpression,
                new CodeMethodInvokeExpression(sizeSliceExpression, new CodeVariableReferenceExpression("Index"),
                    new CodeSnippetExpression($"sizeof(ushort)")));
            statements.Add(new CodeCommentStatement($"Write array size of {fieldName}"));
            statements.Add(new CodeAssignStatement(new CodeVariableReferenceExpression("ArraySize"), readSizeExpression));
            statements.Add(
                new CodeAssignStatement(new CodeVariableReferenceExpression("Index"), new CodeSnippetExpression($"Index + sizeof(ushort)")));
        }

        public static IEnumerable<Type> FindAllSerializableTypes()
        {
            Type targetType = typeof(INetpack);

            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(asm => !asm.IsDynamic)
                .SelectMany(assembly =>
                {
                    try
                    {
                        return assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        return ex.Types.Where(t => t != null)!;
                    }
                })
                .Where(t =>
                    targetType.IsAssignableFrom(t) &&
                    t.IsClass &&
                    !t.IsAbstract)!;
        }
    }
}
