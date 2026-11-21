using CrestApps.OrchardCore.FileStorage.ProtectedFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ProtectedFiles.Azure;

/// <summary>
/// Keeps every protected file area in Azure Blob Storage.
/// </summary>
/// <remarks>
/// The areas move to one Azure Blob Storage container, each area in a folder of its own, when the
/// <c>CrestApps:ProtectedFiles:AzureBlobStorage</c> shell configuration section names a connection string and a
/// container; otherwise they stay in the tenant's App_Data folder.
/// </remarks>
public sealed class Startup : StartupBase
{
    /// <summary>
    /// The shell configuration section the Azure Blob Storage options are read from.
    /// </summary>
    internal const string ConfigurationSectionName = "CrestApps:ProtectedFiles:AzureBlobStorage";

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        // The default (each area in the tenant's App_Data) is what the store falls back to when the section is
        // incomplete, whichever feature registers first.
        services.TryAddSingleton(ProtectedFileAreaStore.Local);
        services.AddTenantFileStore<ProtectedFileAreaStore>(
            "ProtectedFiles",
            ConfigurationSectionName,
            (serviceProvider, fileStore) => new ProtectedFileAreaStore(fileStore),
            removeOnTenantRemoval: true);
    }
}
