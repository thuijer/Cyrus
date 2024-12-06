﻿using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using NForza.Cyrus.Cqrs.Generator.Config;
using NForza.Cyrus.Generators.Cqrs;
using NForza.Generators;

namespace NForza.Cyrus.Cqrs.Generator;

[Generator]
public class CqrsEventConsumerGenerator : CqrsSourceGenerator, IIncrementalGenerator
{
    public override void Initialize(IncrementalGeneratorInitializationContext context)
    {
        DebugThisGenerator(false);
        var configProvider = ParseConfigFile<CyrusConfig>(context, "cyrusConfig.yaml");

        var incrementalValuesProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (syntaxNode, _) => CouldBeEventHandler(syntaxNode),
                transform: (context, _) => GetMethodSymbolFromContext(context));

        var allEventHandlersProvider = incrementalValuesProvider.Combine(configProvider)
            .Where(x =>
            {                
                var (methodNode, config) = x;
                return config.Events.Bus == "MassTransit" && IsEventHandler(methodNode, config.Events.HandlerName, config.Events.Suffix);
            })
            .Select((x, _) => x.Left!)
            .Collect();

        var combinedProvider = allEventHandlersProvider.Combine(context.CompilationProvider);

        context.RegisterSourceOutput(combinedProvider, (spc, eventHandlersWithCompilation) =>
        {
            var (queryHandlers, compilation) = eventHandlersWithCompilation;
            if (queryHandlers.Any())
            {
                var sourceText = GenerateEventConsumers(queryHandlers, compilation);
                spc.AddSource($"EventConsumers.g.cs", SourceText.From(sourceText, Encoding.UTF8));
            }
        });
    }

    private string GenerateEventConsumers(ImmutableArray<IMethodSymbol> handlers, Compilation compilation)
    {       
        StringBuilder source = new();
        foreach (var handler in handlers)
        {
            var methodSymbol = handler;
            var eventType = methodSymbol.Parameters[0].Type;
            source.Append($@"
public class {eventType.Name}Consumer(EventHandlerDictionary eventHandlerDictionary, IServiceScopeFactory serviceScopeFactory) : EventConsumer<{eventType}>(eventHandlerDictionary, serviceScopeFactory)
{{
}}");
        }

        var resolvedSource = TemplateEngine.ReplaceInResourceTemplate("EventConsumers.cs", new Dictionary<string, string>
        {
            ["EventConsumers"] = source.ToString()
        });

        return resolvedSource;
    }
}