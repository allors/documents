// <copyright file="ModelBindingGenerator.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Generators;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Emits a reflection-free <c>ITypedAccessor</c> for every type marked with
/// <c>[Allors.Documents.DocumentModel]</c> and for the model types it references
/// (transitively, within the same assembly). The accessors are registered with
/// <c>AccessorRegistry</c> by a module initializer.
/// </summary>
[Generator]
public sealed class ModelBindingGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName = "Allors.Documents.DocumentModelAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var modelTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeMetadataName,
            predicate: static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax or StructDeclarationSyntax,
            transform: static (syntaxContext, _) => Collect(syntaxContext));

        var collected = modelTypes.Collect();

        context.RegisterSourceOutput(collected, static (productionContext, batches) => Emit(productionContext, batches));
    }

    private static ImmutableArray<ModelType> Collect(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol root)
        {
            return ImmutableArray<ModelType>.Empty;
        }

        var assembly = root.ContainingAssembly;
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var queue = new Queue<INamedTypeSymbol>();
        var result = ImmutableArray.CreateBuilder<ModelType>();

        Enqueue(root);

        while (queue.Count > 0)
        {
            var type = queue.Dequeue();

            var memberNames = new List<string>();
            var seenNames = new HashSet<string>();

            for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
            {
                foreach (var member in current.GetMembers())
                {
                    switch (member)
                    {
                        case IPropertySymbol { IsStatic: false, IsIndexer: false, DeclaredAccessibility: Accessibility.Public } property
                            when property.GetMethod is { DeclaredAccessibility: Accessibility.Public } && seenNames.Add(property.Name):
                            memberNames.Add(property.Name);
                            EnqueueReferenced(property.Type);
                            break;

                        case IFieldSymbol { IsStatic: false, IsConst: false, IsImplicitlyDeclared: false, DeclaredAccessibility: Accessibility.Public } field
                            when seenNames.Add(field.Name):
                            memberNames.Add(field.Name);
                            EnqueueReferenced(field.Type);
                            break;
                    }
                }
            }

            var fullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // The fully qualified name @-escapes keyword identifiers; strip the escapes so the
            // mangled name stays a valid identifier.
            var safeName = fullyQualifiedName.Replace("global::", string.Empty).Replace("@", string.Empty).Replace('.', '_');

            result.Add(new ModelType(fullyQualifiedName, safeName, string.Join(";", memberNames)));
        }

        return result.ToImmutable();

        void Enqueue(INamedTypeSymbol candidate)
        {
            if (candidate.TypeParameters.Length == 0 && seen.Add(candidate))
            {
                queue.Enqueue(candidate);
            }
        }

        void EnqueueReferenced(ITypeSymbol type)
        {
            switch (type)
            {
                case IArrayTypeSymbol array:
                    EnqueueReferenced(array.ElementType);
                    break;

                case INamedTypeSymbol named:
                    if (named.IsGenericType)
                    {
                        foreach (var argument in named.TypeArguments)
                        {
                            EnqueueReferenced(argument);
                        }

                        break;
                    }

                    if (named.SpecialType == SpecialType.None &&
                        named.TypeKind is TypeKind.Class or TypeKind.Struct &&
                        !named.IsAnonymousType &&
                        SymbolEqualityComparer.Default.Equals(named.ContainingAssembly, assembly))
                    {
                        Enqueue(named);
                    }

                    break;
            }
        }
    }

    private static void Emit(SourceProductionContext context, ImmutableArray<ImmutableArray<ModelType>> batches)
    {
        var modelTypes = batches
            .SelectMany(batch => batch)
            .GroupBy(modelType => modelType.FullyQualifiedName)
            .Select(group => group.First())
            .OrderBy(modelType => modelType.FullyQualifiedName)
            .ToList();

        if (modelTypes.Count == 0)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.Append("""
            // <auto-generated/>
            #nullable enable

            namespace Allors.Documents.Generated
            {
                internal static class DocumentModelAccessors
                {
                    [global::System.Runtime.CompilerServices.ModuleInitializer]
                    internal static void Initialize()
                    {

            """);

        foreach (var modelType in modelTypes)
        {
            builder.Append("            global::Allors.Documents.Expressions.AccessorRegistry.Register(typeof(")
                .Append(modelType.FullyQualifiedName)
                .Append("), new ")
                .Append(modelType.SafeName)
                .Append("Accessor());\n");
        }

        builder.Append("""
                    }

            """);

        foreach (var modelType in modelTypes)
        {
            builder.Append("        private sealed class ")
                .Append(modelType.SafeName)
                .Append("""
                    Accessor : global::Allors.Documents.Expressions.ITypedAccessor
                            {
                                public bool TryGet(object instance, string member, out object? value)
                                {

                    """);

            var memberNames = modelType.MemberNames.Length == 0
                ? System.Array.Empty<string>()
                : modelType.MemberNames.Split(';');

            if (memberNames.Length > 0)
            {
                builder.Append("                var typed = (").Append(modelType.FullyQualifiedName).Append(")instance;\n");
                builder.Append("                switch (member)\n                {\n");

                foreach (var memberName in memberNames)
                {
                    // The case label is the raw name, as used by templates and the reflection
                    // fallback; the member access must escape keyword identifiers.
                    builder.Append("                    case \"")
                        .Append(memberName)
                        .Append("\": value = typed.")
                        .Append(EscapeIdentifier(memberName))
                        .Append("; return true;\n");
                }

                builder.Append("                }\n\n");
            }

            builder.Append("""
                                value = null;
                                return false;
                            }
                        }

            """);
        }

        builder.Append("""
                }
            }
            """);

        context.AddSource("DocumentModelAccessors.g.cs", SourceText.From(builder.ToString(), Encoding.UTF8));
    }

    private static string EscapeIdentifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None
            ? "@" + name
            : name;

    private sealed record ModelType(string FullyQualifiedName, string SafeName, string MemberNames);
}
