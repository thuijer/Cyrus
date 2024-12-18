﻿using Microsoft.Extensions.DependencyInjection;

namespace NForza.Cyrus.WebApi;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEndpointGroup<T>(this IServiceCollection services)
        where T : EndpointGroup
      => services.AddTransient<EndpointGroup, T>();
}
