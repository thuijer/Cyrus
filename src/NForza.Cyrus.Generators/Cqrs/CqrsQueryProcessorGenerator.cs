﻿using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using NForza.Cyrus.Generators;
using NForza.Generators;

namespace NForza.Cyrus.Cqrs.Generator;

[Generator]
public class CqrsQueryHandlerGenerator : GeneratorBase, IIncrementalGenerator
{
    public override void Initialize(IncrementalGeneratorInitializationContext context)
    {
        DebugThisGenerator(false);
        var configProvider = ConfigProvider(context);

        var incrementalValuesProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (syntaxNode, _) => CouldBeQueryHandler(syntaxNode),
                transform: (context, _) => GetMethodSymbolFromContext(context));

        var allQueryHandlersProvider = incrementalValuesProvider.Combine(configProvider)
            .Where(x =>
            {
                var (methodNode, config) = x;
                return IsQueryHandler(methodNode, config.Queries.HandlerName, config.Queries.Suffix);
            })
            .Select((x, _) => x.Left!)
            .Collect();

        var combinedProvider = allQueryHandlersProvider.Combine(context.CompilationProvider);

        context.RegisterSourceOutput(combinedProvider, (spc, queryHandlersWithCompilation) =>
        {
            var (queryHandlers, compilation) = queryHandlersWithCompilation;
            if (queryHandlers.Any())
            {
                var sourceText = GenerateQueryProcessorExtensionMethods(queryHandlers, compilation);
                spc.AddSource($"QueryProcessor.g.cs", SourceText.From(sourceText, Encoding.UTF8));
            }
        });
    }

    private string GenerateQueryProcessorExtensionMethods(ImmutableArray<IMethodSymbol> handlers, Compilation compilation)
    {
        INamedTypeSymbol? taskSymbol = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
        if (taskSymbol == null)
        {
            return string.Empty;
        }
        StringBuilder source = new();
        foreach (var handler in handlers)
        {
            var methodSymbol = handler;
            var parameterType = methodSymbol.Parameters[0].Type.ToFullName();
            var returnType = (INamedTypeSymbol)methodSymbol.ReturnType;
            string returnTypeFullName = returnType.ToFullName();
            var isAsync = returnType.OriginalDefinition.Equals(taskSymbol, SymbolEqualityComparer.Default);

            if (isAsync)
            {
                var queryType = (INamedTypeSymbol)returnType.TypeArguments[0];
                source.Append($@"
    public static {returnTypeFullName} Query(this IQueryProcessor queryProcessor, {parameterType} command, CancellationToken cancellationToken = default) 
        => queryProcessor.QueryInternal<{parameterType}, {queryType}>(command, cancellationToken);");
            }
            else
            {
                source.Append($@"
    public static Task<{returnTypeFullName}> Query(this IQueryProcessor queryProcessor, {parameterType} command, CancellationToken cancellationToken = default) 
        => queryProcessor.QueryInternal<{parameterType}, {returnTypeFullName}>(command, cancellationToken);");
            }
        }

        var resolvedSource = TemplateEngine.ReplaceInResourceTemplate("QueryProcessorExtensions.cs", new Dictionary<string, string>
        {
            ["QueryMethods"] = source.ToString()
        });

        return resolvedSource;
    }
}