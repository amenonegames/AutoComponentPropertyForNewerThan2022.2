using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Amenonegames.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.DotnetRuntime.Extensions;
using Microsoft.CodeAnalysis.Text;


namespace Amenonegames.AutoComponentProperty
{
    [Generator]
    public class AutoComponentPropertyGenerator : IIncrementalGenerator
    {
        
        public void Initialize(IncrementalGeneratorInitializationContext  context)
        {
            context.RegisterPostInitializationOutput( static x => SetDefaultAttribute(x));
            var provider = context.SyntaxProvider.ForAttributeWithMetadataName
                (
                    context,
                    "AutoComponentProperty.CompoPropAttribute",
                    static (node, cancellation) => true,//node is FieldDeclarationSyntax,
                    static (cont, cancellation) => cont
                )
                .Combine(context.CompilationProvider)
                .WithComparer(Comparer.Instance);
            
            
            context.RegisterSourceOutput(
                context.CompilationProvider.Combine(provider.Collect()),
                static (sourceProductionContext, t) =>
                {

                    
                    var (compilation, list) = t;
                    
                    
                    var references = ReferenceSymbols.Create(compilation);
                    if (references is null)
                    {
                        return;
                    }
                    
                    var codeWriter = new CodeWriter();
                    var typeMetaList = new List<VariableTypeMeta>();
                    
                    foreach (var (x,y) in list)
                    {

                            typeMetaList.Add
                            (
                                new VariableTypeMeta(y,
                                    (VariableDeclaratorSyntax)x.TargetNode,
                                    (IFieldSymbol)x.TargetSymbol,
                                    x.Attributes,
                                    references)
                            );
                    }
                    
                    var classGrouped = typeMetaList.GroupBy( x  => x.ClassSymbol);
                    
                    foreach (var classed in classGrouped)
                    {
                        if (TryEmit(classed, codeWriter, references, sourceProductionContext))
                        {
                            var className = classed.Key.Name;
                            sourceProductionContext.AddSource($"{className}.g.cs", codeWriter.ToString());
                        }
                        codeWriter.Clear();
                    }
                    
                }); 
        }
        

        static bool TryEmit(
            IGrouping<INamedTypeSymbol,VariableTypeMeta>? typeMetaGroup,
            CodeWriter codeWriter,
            ReferenceSymbols references,
            in SourceProductionContext context)
        {
            VariableTypeMeta[] variableTypeMetas = new VariableTypeMeta[] { };
            INamedTypeSymbol classSymbol = null;
            ClassDeclarationSyntax? classSyntax = null;
            var error = false;
            
            try
            {
                if (typeMetaGroup is not null)
                {
                    variableTypeMetas = typeMetaGroup.ToArray();
                    // 親クラスを取得
                    classSymbol = typeMetaGroup.Key;
                    classSyntax = typeMetaGroup.First().ClassSyntax;
                }
            
                if (classSymbol is null || classSyntax is null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ClassNotFound,
                        variableTypeMetas.First().Syntax.GetLocation(),
                        String.Join("/",variableTypeMetas.Select(x => x.Syntax.ToString()))));
                    error = true;
                }
                // verify is partial
                else if (!classSyntax.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.MustBePartial,
                        classSyntax.Identifier.GetLocation(),
                        classSyntax.Identifier.Text));
                    error = true;
                }
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.UnexpectedErrorDescriptor,
                    Location.None,
                    ex.ToString()));
                return false;
            }
            

            try
            {
                var nameSpaceIsGlobal = classSymbol != null && classSymbol.ContainingNamespace.IsGlobalNamespace;
                var nameSpaceStr = nameSpaceIsGlobal ? "" : $"namespace {classSymbol.ContainingNamespace.ToDisplayString()}";
                var classAccessiblity = classSymbol?.DeclaredAccessibility.ToString().ToLower();
                
                codeWriter.AppendLine(nameSpaceStr);
                if(!nameSpaceIsGlobal) codeWriter.BeginBlock();
                
                codeWriter.AppendLine("// This class is generated by AutoPropertyGenerator.");
                codeWriter.AppendLine($"{classAccessiblity} partial class {classSymbol?.Name}");
                codeWriter.BeginBlock();

                foreach (var variableTypeMeta in variableTypeMetas)
                {

                    var sourceClassName = variableTypeMeta?.SourceType?.ToDisplayString();
                    if (variableTypeMeta?.SourceType?.Name is null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.VaribleNameNotFound,
                            variableTypeMeta.Syntax.GetLocation(),
                            String.Join("/", variableTypeMeta.Syntax.ToString())));
                        error = true;
                    }

                    var originalVariableName = variableTypeMeta?.Syntax.Identifier.ValueText;
                    var propertyName = GetPropertyName(originalVariableName);
                    var rowClassName = sourceClassName;
                    if (sourceClassName.EndsWith("[]"))
                    {
                        rowClassName = sourceClassName.Substring(0, sourceClassName.Length - 2);
                    }

                    codeWriter.AppendLineBreak(false);
                    codeWriter.Append(
                        $"private {sourceClassName} {propertyName} ");
                    codeWriter.AppendLineBreak(false);
                    codeWriter.BeginBlock();
                    codeWriter.Append($"get");
                    codeWriter.AppendLineBreak(false);
                    codeWriter.BeginBlock();
                    codeWriter.Append($"if ({originalVariableName} == null)");
                    codeWriter.AppendLineBreak(false);
                    codeWriter.BeginBlock();
                    
                    // codeWriter.AppendLineBreak(false);
                    // codeWriter.Append($"{originalVariableName} = ");
                    
                    var from = variableTypeMeta?.GetFromArgument ?? GetFrom.This;
                    var isArray = variableTypeMeta?.IsSourceTypeArray ?? false;

                    if (from == GetFrom.This && !isArray)
                        codeWriter.Append($"return {originalVariableName} = GetComponent<{sourceClassName}>();");
                    else if (from == GetFrom.Children && !isArray)
                        codeWriter.Append($"return {originalVariableName} = GetComponentInChildren<{sourceClassName}>(true);");
                    else if (from == GetFrom.Parent && !isArray)
                        codeWriter.Append($"return {originalVariableName} = GetComponentInParent<{sourceClassName}>(true);");
                    else if (from == GetFrom.This && isArray)
                        codeWriter.Append($"return {originalVariableName} = GetComponents<{rowClassName}>();");
                    else if (from == GetFrom.Children && isArray)
                        codeWriter.Append($"return {originalVariableName} = GetComponentsInChildren<{rowClassName}>(true);");
                    else if (from == GetFrom.Parent && isArray)
                        codeWriter.Append($"return {originalVariableName} = GetComponentsInParent<{rowClassName}>(true);");
                    else
                        codeWriter.Append($"return {originalVariableName} = GetComponent<{sourceClassName}>();");

                    codeWriter.AppendLineBreak(false);
                    // codeWriter.Append($": {originalVariableName};");
                    codeWriter.EndBlock();
                    codeWriter.Append($"else return {originalVariableName};");
                    
                    codeWriter.AppendLineBreak(false);
                    codeWriter.EndBlock();
                    codeWriter.AppendLineBreak(false);
                    codeWriter.EndBlock();
                    
                    // codeWriter.AppendLineBreak(false);
                    // codeWriter.AppendLineBreak(false);
                }
                
                codeWriter.EndBlock();
                if(!nameSpaceIsGlobal) codeWriter.EndBlock();

                return true;
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.UnexpectedErrorDescriptor,
                    Location.None,
                    ex.ToString()));
                return false;
            }
            
        }


        static string? GetNamespace(ClassDeclarationSyntax classDeclaration)
        {
            var current = classDeclaration.Parent;
            while (current != null)
            {
                if (current is NamespaceDeclarationSyntax namespaceDeclaration)
                {
                    return namespaceDeclaration.Name.ToString();
                }
                current = current.Parent;
            }

            return null; // グローバル名前空間にある場合
        }
        
        private static void SetDefaultAttribute(IncrementalGeneratorPostInitializationContext context)
        {
            // AutoPropertyAttributeのコード本体
            const string AttributeText = @"
using System;
namespace AutoComponentProperty
{
    [AttributeUsage(AttributeTargets.Field,
                    Inherited = false, AllowMultiple = false)]
        sealed class CompoPropAttribute : Attribute
    {
    
        public GetFrom from { get; set; }

        public CompoPropAttribute(GetFrom from = GetFrom.This)
        {   
            this.from = from;
        }
        
    }

    [Flags]
    internal enum GetFrom
    {
        This  = 1,
        Children = 1 << 1,
        Parent = 1 << 2,
    }

}
";                
            //コンパイル時に参照するアセンブリを追加
            context.AddSource
            (
                "CompoPropAttribute.cs",
                SourceText.From(AttributeText,Encoding.UTF8)
            );
        }
        
        private static string GetPropertyName(string fieldName)
        {
            
            // 最初の大文字に変換可能な文字を探す
            for (int i = 0; i < fieldName.Length; i++)
            {
                if (char.IsLower(fieldName[i]))
                {
                    // 大文字に変換して、残りの文字列を結合
                    return char.ToUpper(fieldName[i]) + fieldName.Substring(i + 1);
                }
            }

            // 大文字に変換可能な文字がない場合
            return "NoLetterCanUppercase";
        }

        
    }

    class Comparer : IEqualityComparer<(GeneratorAttributeSyntaxContext, Compilation)>
    {
        public static readonly Comparer Instance = new();

        public bool Equals((GeneratorAttributeSyntaxContext, Compilation) x, (GeneratorAttributeSyntaxContext, Compilation) y)
        {
            return x.Item1.TargetNode.Equals(y.Item1.TargetNode);
        }

        public int GetHashCode((GeneratorAttributeSyntaxContext, Compilation) obj)
        {
            return obj.Item1.TargetNode.GetHashCode();
        }
    }
}