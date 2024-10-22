﻿using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace NForza.Cqrs.WebApi;

public class DefaultQueryInputMappingPolicy(HttpContext httpContext) : InputMappingPolicy
{
    private static readonly Dictionary<Type, Func<string, object>> TypeConverters = new()
    {
        { typeof(Guid), s => Guid.Parse(s) },
        { typeof(int), s => int.Parse(s) },
        { typeof(long), s => long.Parse(s) },
        { typeof(string), s => s }
    };

    public override Task<object> MapInputAsync(Type typeToCreate)
    {
        ConstructorInfo FindConstructorForQuery(Type queryType)
        {
            if (queryType.GetConstructors().Length > 1)
                throw new ArgumentException($"More than one constructor found for query {queryType.Name}.");
            return queryType.GetConstructors().First();
        }

        var constructor = FindConstructorForQuery(typeToCreate);
        List<object?> parameters = new();
        foreach (var parameter in constructor.GetParameters())
            if (httpContext.Request.RouteValues.TryGetValue(parameter.Name!, out var value))
                parameters.Add(value == null ? null : TypeConverters[parameter.ParameterType](value.ToString()!));
        object? result = constructor.Invoke(parameters.ToArray());
        return Task.FromResult(result);
    }
}
