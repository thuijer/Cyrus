﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using NForza.Generators;

namespace NForza.TypedIds.Generator;

[Generator]
public class TypedIdServiceCollectionGenerator : TypedIdGeneratorBase, ISourceGenerator
{
    public override void Execute(GeneratorExecutionContext context)
    {
        DebugThisGenerator(false);

        var typedIds =
            GetAllTypedIds(context.Compilation, "StringIdAttribute").Concat(GetAllTypedIds(context.Compilation, "GuidIdAttribute"));
        GenerateServiceCollectionExtensionMethod(context, typedIds);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1035:Do not use APIs banned for analyzers", Justification = "Environment.NewLine should not be a banned API.")]
    private void GenerateServiceCollectionExtensionMethod(GeneratorExecutionContext context, IEnumerable<INamedTypeSymbol> typedIds)
    {
        var source = EmbeddedResourceReader.GetResource(Assembly.GetExecutingAssembly(), "Templates", "ServiceCollectionExtensions.cs");

        var converters = string.Join(Environment.NewLine, typedIds.Select(t => $"services.AddTransient<JsonConverter, {t.Name}JsonConverter>();"));

        var namespaces = string.Join(Environment.NewLine, typedIds.Select(t => t.ContainingNamespace.ToDisplayString()).Distinct().Select(ns => $"using {ns};"));

        var types = string.Join(",", typedIds.Select(t => $"[typeof({t.ToDisplayString()})] = typeof({GetUnderlyingTypeOfTypedId(t)})"));
        var registrations = string.Join(Environment.NewLine, typedIds.Select(t => $"services.AddTransient<{t.ToDisplayString()}>();"));

        source = source
            .Replace("% AllTypes %", types)
            .Replace("% AllTypedIdRegistrations %", registrations)
            .Replace("% Namespaces %", namespaces)
            .Replace("% AddJsonConverters %", converters);
        context.AddSource($"ServiceCollectionExtensions.g.cs", source);
    }
}