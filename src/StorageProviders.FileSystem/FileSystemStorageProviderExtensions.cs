using Microsoft.Extensions.DependencyInjection;

namespace StorageProviders.FileSystem;

/// <summary>
/// Provides dependency-injection registration methods for the local file-system implementation of <see cref="IStorageProvider" />.
/// </summary>
public static class FileSystemStorageProviderExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="IStorageProvider" /> as a singleton backed by the local file system using settings configured at startup.
        /// </summary>
        /// <param name="optionsAction">The configuration delegate used to populate the shared <see cref="FileSystemStorageSettings" /> instance.</param>
        /// <returns>The same <see cref="IServiceCollection" /> instance so registrations can be chained fluently.</returns>
        public IServiceCollection AddFileSystemStorage(Action<FileSystemStorageSettings> optionsAction)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(optionsAction);

            var settings = new FileSystemStorageSettings();
            optionsAction.Invoke(settings);

            services.AddSingleton(settings);
            services.AddSingleton<IStorageProvider, FileSystemStorageProvider>();

            return services;
        }

        /// <summary>
        /// Registers <see cref="IStorageProvider" /> as a scoped file-system provider using settings built from the current <see cref="IServiceProvider" />.
        /// </summary>
        /// <param name="optionsAction">The configuration delegate used to populate <see cref="FileSystemStorageSettings" /> with access to services from the current scope.</param>
        /// <returns>The same <see cref="IServiceCollection" /> instance so registrations can be chained fluently.</returns>
        public IServiceCollection AddFileSystemStorage(Action<IServiceProvider, FileSystemStorageSettings> optionsAction)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(optionsAction);

            services.AddScoped(provider =>
            {
                var settings = new FileSystemStorageSettings();
                optionsAction.Invoke(provider, settings);
                return settings;
            });

            services.AddScoped<IStorageProvider, FileSystemStorageProvider>();

            return services;
        }
    }
}
