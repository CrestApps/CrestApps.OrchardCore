using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.FileStorage.ProtectedFiles;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.FileStorage;
using OrchardCore.FileStorage.FileSystem;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers protected file areas.
/// </summary>
public static class ProtectedFileServiceCollectionExtensions
{
    /// <summary>
    /// Registers an area of protected files kept in a folder of the tenant's App_Data, with its signed links.
    /// </summary>
    /// <typeparam name="TArea">The marker type that names the area.</typeparam>
    /// <param name="services">The services.</param>
    /// <param name="folder">The folder under the tenant's App_Data.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddProtectedFileArea<TArea>(this IServiceCollection services, string folder)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(folder);

        services.Configure<ProtectedFileAreaOptions<TArea>>(options => options.Folder = folder);
        services.TryAddTransient<FileCreationService>();
        services.TryAddScoped<IProtectedFileStore<TArea>>(serviceProvider =>
        {
            var shellOptions = serviceProvider.GetRequiredService<IOptions<ShellOptions>>().Value;
            var shellSettings = serviceProvider.GetRequiredService<ShellSettings>();
            var path = Path.Combine(shellOptions.ShellsApplicationDataPath, shellOptions.ShellsContainerName, shellSettings.Name, folder);

            return new ProtectedFileStore<TArea>(
                new FileSystemStore(path, serviceProvider.GetRequiredService<ILogger<FileSystemStore>>()),
                serviceProvider.GetRequiredService<FileCreationService>());
        });
        services.TryAddScoped<IProtectedFileLinks<TArea>, ProtectedFileLinks<TArea>>();

        return services;
    }
}
