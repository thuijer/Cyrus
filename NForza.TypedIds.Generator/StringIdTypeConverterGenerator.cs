﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Microsoft.CodeAnalysis;
using NForza.Generators;

namespace NForza.TypedIds.Generator;

[Generator]
public class StringIdTypeConverterGenerator : TypedIdGeneratorBase, ISourceGenerator
{
 
    public override void Execute(GeneratorExecutionContext context)
    {
#if DEBUG_ANALYZER 
        if (!Debugger.IsAttached && false)
        {
            Debugger.Launch();
        }
#endif
        var stringIds =
            GetAllTypedIds(context.Compilation, "StringIdAttribute");
        foreach (var item in stringIds)
        {
            GenerateStringIdTypeConverter(context, item);
        }
    }

    private void GenerateStringIdTypeConverter(GeneratorExecutionContext context, INamedTypeSymbol item)
    {
        string fullyQualifiedNamespace = item.ContainingNamespace.ToDisplayString();
        var replacements = new Dictionary<string, string> {
            ["TypedIdName"] = item.Name,
            ["NamespaceName"] = fullyQualifiedNamespace
        };
        var resolvedSource = TemplateEngine.ReplaceInResourceTemplate("StringIdTypeConverter.cs", replacements);
        context.AddSource($"{item}TypeConverter.g.cs", resolvedSource);
    }
}