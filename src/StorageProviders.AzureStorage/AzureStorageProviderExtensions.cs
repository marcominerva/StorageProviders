using Microsoft.Extensions.DependencyInjection;

namespace StorageProviders.AzureStorage;

/// <summary>
/// Provides dependency-injection registration methods for the Azure Blob Storage implementation of <see cref="IStorageProvider" />.
/// </summary>
/// <remarks>
/// These extensions keep application startup code independent from the concrete <see cref="AzureStorageProvider" /> type while allowing callers to configure the Azure Storage connection details in the same place they configure other services.
/// </remarks>
public static class AzureStorageProviderExtensions
{
    /// <summary>
    /// Registers <see cref="IStorageProvider" /> as a singleton backed by Azure Blob Storage using settings configured at startup.
    /// </summary>
    /// <param name="services">The service collection that receives the Azure Storage provider registration.</param>
    /// <param name="optionsAction">The configuration delegate used to populate the <see cref="AzureStorageSettings" /> instance shared by the provider.</param>
    /// <returns>The same <see cref="IServiceCollection" /> instance so registrations can be chained fluently.</returns>
    /// <remarks>
    /// Use this overload when the connection settings are known during service registration and do not depend on scoped services.
    /// </remarks>
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

    /// <summary>
    /// Registers <see cref="IStorageProvider" /> as a scoped Azure Blob Storage provider using settings that can be built from the current <see cref="IServiceProvider" />.
    /// </summary>
    /// <param name="services">The service collection that receives the Azure Storage provider registration.</param>
    /// <param name="optionsAction">The configuration delegate used to populate <see cref="AzureStorageSettings" /> with access to services from the current scope.</param>
    /// <returns>The same <see cref="IServiceCollection" /> instance so registrations can be chained fluently.</returns>
    /// <remarks>
    /// Use this overload when storage settings are resolved from other registered services, such as tenant-aware configuration or scoped options providers.
    /// </remarks>
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
