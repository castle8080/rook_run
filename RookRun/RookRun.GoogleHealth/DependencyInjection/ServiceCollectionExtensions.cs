using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace RookRun.GoogleHealth.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGoogleHealth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<GHExportActivityExtractor, GHExportActivityExtractor>();
        return services;
    }
}
