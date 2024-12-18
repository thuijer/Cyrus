﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using NForza.Generators;

namespace NForza.Cyrus.TypedIds.Generator;

[Generator]
public class TypedIdJsonConverterGenerator : TypedIdGeneratorBase, IIncrementalGenerator
{
    public override void Initialize(IncrementalGeneratorInitializationContext context)
    {
        DebugThisGenerator(false);
        var incrementalValuesProvider = context.SyntaxProvider
                     .CreateSyntaxProvider(
                         predicate: (syntaxNode, _) => IsRecordWithStringIdAttribute(syntaxNode) || IsRecordWithGuidIdAttribute(syntaxNode) || IsRecordWithIntIdAttribute(syntaxNode),
                         transform: (context, _) => GetNamedTypeSymbolFromContext(context));
        var typedIdsProvider = incrementalValuesProvider
            .Where(x => x is not null)
            .Select((x, _) => x!)
            .Collect();

        context.RegisterSourceOutput(typedIdsProvider, (spc, typedIds) =>
        {
            foreach (var typedId in typedIds)
            {
                var sourceText = GenerateJsonConverterForTypedId(typedId);
                spc.AddSource($"{typedId.Name}.g.cs", SourceText.From(sourceText, Encoding.UTF8));
            };
        });
    }

    private string GenerateJsonConverterForTypedId(INamedTypeSymbol item)
    {
        string fullyQualifiedNamespace = item.ContainingNamespace.ToDisplayString();
        var underlyingTypeName = GetUnderlyingTypeOfTypedId(item);

        string? getMethodName = underlyingTypeName switch
        {
            "System.Guid" => "GetGuid",
            "string" => "GetString",
            "int" => "GetInt32",
            _ => throw new NotSupportedException($"Underlying type {underlyingTypeName} is not supported.")
        };

        string? templateName = underlyingTypeName switch
        {
            "System.Guid" => "GuidIdJsonConverter.cs",
            "string" => "StringIdJsonConverter.cs",
            "int" => "IntIdJsonConverter.cs",
            _ => throw new NotSupportedException($"Underlying type {underlyingTypeName} is not supported.")
        };

        var replacements = new Dictionary<string, string>
        {
            ["TypedIdName"] = item.Name,
            ["NamespaceName"] = fullyQualifiedNamespace,
            ["GetMethodName"] = getMethodName
        };
        var source = TemplateEngine.ReplaceInResourceTemplate(templateName, replacements);
        return source;
    }
}