using System;
using AzureStorageProvider.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace AzureStorageProvider
{
    public static class AzureStorageProviderExtensions
    {
        public static IServiceCollection AddAzureStorage(this IServiceCollection services, Action<AzureStorageSettings> configuration)
        {
            services.Configure(configuration);

            services.AddScoped<IStorageProvider, AzureStorageProvider>();
            return services;
        }
    }
}
