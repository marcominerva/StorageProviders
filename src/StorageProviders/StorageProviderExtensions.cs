using System;
using Microsoft.Extensions.DependencyInjection;

namespace AzureStorageProvider
{
    public static class StorageProviderExtensions
    {
        public static IServiceCollection AddAzureStorage(this IServiceCollection services, Action<AzureStorageSettings> configuration)
        {
            services.Configure(configuration);

            services.AddScoped<IAzureStorageProvider, AzureStorageProvider>();
            return services;
        }
    }
}
