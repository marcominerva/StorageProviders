using Microsoft.Extensions.DependencyInjection;
using StorageProvider.Abstractions;

namespace AzureStorageProvider;

public static class AzureStorageProviderExtensions
{
    public static IServiceCollection AddAzureStorage(this IServiceCollection services, Action<AzureStorageSettings> configuration)
    {
        var azureStorageSettings = new AzureStorageSettings();
        configuration?.Invoke(azureStorageSettings);

        services.AddSingleton(azureStorageSettings);
        services.AddScoped<IStorageProvider, AzureStorageProvider>();
        return services;
    }
}
