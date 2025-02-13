using Microsoft.Extensions.DependencyInjection;

namespace StorageProviders.AzureStorage;

public static class AzureStorageProviderExtensions
{
    public static IServiceCollection AddAzureStorage(this IServiceCollection services, Action<AzureStorageSettings> optionsAction)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(optionsAction);

        var azureStorageSettings = new AzureStorageSettings();
        optionsAction.Invoke(azureStorageSettings);

        services.AddSingleton(azureStorageSettings);
        services.AddSingleton<IStorageProvider, AzureStorageProvider>();

        return services;
    }

    public static IServiceCollection AddAzureStorage(this IServiceCollection services, Action<IServiceProvider, AzureStorageSettings> optionsAction)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(optionsAction);

        services.AddScoped(provider =>
        {
            var azureStorageSettings = new AzureStorageSettings();
            optionsAction.Invoke(provider, azureStorageSettings);
            return azureStorageSettings;
        });

        services.AddScoped<IStorageProvider, AzureStorageProvider>();

        return services;
    }
}
