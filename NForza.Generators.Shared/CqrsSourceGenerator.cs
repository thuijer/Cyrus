﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NForza.Cqrs.Generator.Config;
using NForza.Generators;

#pragma warning disable RS1035 // Do not use banned APIs for analyzers

namespace NForza.Cqrs.Generator;

public abstract class CqrsSourceGenerator : GeneratorBase
{
    private CqrsConfig configuration;
    protected CqrsConfig Configuration => configuration;

    protected List<IMethodSymbol> GetAllCommandHandlers(GeneratorExecutionContext context, string methodHandlerName, List<INamedTypeSymbol> commands) =>
        context.Compilation
            .GetSymbolsWithName(s => s == methodHandlerName, SymbolFilter.Member)
            .OfType<IMethodSymbol>()
            .Where(m => m.Parameters.Length == 1 && commands.Contains(m.Parameters[0].Type, SymbolEqualityComparer.Default))
            .Where(m => ReturnsTaskOfCommandResult(context, m))
            .ToList();

    protected List<IMethodSymbol> GetAllQueryHandlers(GeneratorExecutionContext context, string methodHandlerName, List<INamedTypeSymbol> queries)
    {
        var symbolsWithCorrectName = context.Compilation.GetSymbolsWithName(s => s == methodHandlerName, SymbolFilter.Member);
        var methodsWithCorrectName = symbolsWithCorrectName.OfType<IMethodSymbol>();
        var methodsWithCorrectNameAndParameters = methodsWithCorrectName
            .Where(m => HasQueryHandlerASignature(m, queries));
        return methodsWithCorrectNameAndParameters.ToList();

        static bool HasQueryHandlerASignature(IMethodSymbol method, List<INamedTypeSymbol> queries)
        {
            return method.Parameters.Length <= 2
                               &&
                               queries.Contains(method.Parameters[0].Type, SymbolEqualityComparer.Default)
                               &&
                               (method.Parameters.Length == 1 || method.Parameters[1].Name == "CancellationToken");
        }
    }

    private IEnumerable<INamedTypeSymbol> GetAllTypesWithSuffix(Compilation compilation, IEnumerable<string> contractProjectSuffixes, string typeSuffix)
    {
        var typesInCurrentCompilation = compilation.GetSymbolsWithName(s => s.EndsWith(typeSuffix), SymbolFilter.Type).OfType<INamedTypeSymbol>();
        foreach (var t in typesInCurrentCompilation)
            yield return t;

        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assemblySymbol
                ||
                assemblySymbol.Name.StartsWith("System")
                ||
                assemblySymbol.Name.StartsWith("Microsoft")
                ||
                !contractProjectSuffixes.Any(assemblySymbol.Name.EndsWith))
                continue;

            var allTypes = GetAllTypes(assemblySymbol.GlobalNamespace);

            foreach (var t in allTypes)
            {
                if (t.Name.EndsWith(typeSuffix))
                    yield return t;
            }
        }
    }

    static bool IsStruct(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.IsValueType && typeSymbol.TypeKind == TypeKind.Struct;
    }

    protected IEnumerable<INamedTypeSymbol> GetAllQueries(Compilation compilation, IEnumerable<string> contractProjectSuffixes, string querySuffix)
    {
        var queriesInDomain = GetAllTypesWithSuffix(compilation, contractProjectSuffixes, querySuffix)
            .ToList();

        return queriesInDomain;
    }

    protected IEnumerable<INamedTypeSymbol> GetAllCommands(Compilation compilation, IEnumerable<string> contractProjectSuffixes, string commandSuffix)
    {
        var commandsInDomain = GetAllTypesWithSuffix(compilation, contractProjectSuffixes, commandSuffix)
            .Where(t => IsStruct(t));

        return commandsInDomain;
    }

    private static bool ReturnsTaskOfCommandResult(GeneratorExecutionContext context, IMethodSymbol handlerType)
    {
        Debug.WriteLine($"Found handler: {handlerType.Name}");

        if (IsTaskOfCommandResult(handlerType.ReturnType, context.Compilation))
        {
            return true;
        }
        else
        {
            context.ReportDiagnostic(Diagnostic.Create(
                 new DiagnosticDescriptor(
                     "SG0001",
                     "Incorrect return type for command handler",
                     "Method {0} returns {1}. All methods must return Task<CommandResult>.",
                     "NForza.Cqrs",
                     DiagnosticSeverity.Error,
                     true), handlerType.Locations.FirstOrDefault(), handlerType.Name, handlerType.ReturnType.Name));
            return false;
        }
    }

    private static bool IsTaskOfCommandResult(ITypeSymbol returnType, Compilation compilation)
    {
        INamedTypeSymbol taskSymbol = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
        if (returnType is INamedTypeSymbol namedTypeSymbol)
        {
            if (namedTypeSymbol.IsGenericType &&
                    namedTypeSymbol.ConstructedFrom.Equals(taskSymbol, SymbolEqualityComparer.Default))
            {

                var genericArguments = namedTypeSymbol.TypeArguments;
                if (genericArguments.Length == 1)
                {
                    var commandResultSymbol = compilation.GetTypeByMetadataName("NForza.Cqrs.CommandResult");
                    return SymbolEqualityComparer.Default.Equals(genericArguments[0], commandResultSymbol);
                }
            }
        }
        return false;
    }

    protected IEnumerable<INamedTypeSymbol> GetAllClassesDerivedFrom(Compilation compilation, string className)
    {
        var baseTypeSymbol = compilation.GetTypeByMetadataName(className);

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            var classDeclarations = syntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>();

            foreach (var classDeclaration in classDeclarations)
            {
                var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

                if (classSymbol != null && classSymbol.BaseType != null)
                {
                    if (classSymbol.BaseType.Equals(baseTypeSymbol, SymbolEqualityComparer.Default))
                    {
                        yield return classSymbol;
                    }
                }
            }
        }
    }

    public override void Execute(GeneratorExecutionContext context)
    {
        configuration ??= ParseConfigFile<CqrsConfig>(context, "cqrsConfig.yaml");
    }
}