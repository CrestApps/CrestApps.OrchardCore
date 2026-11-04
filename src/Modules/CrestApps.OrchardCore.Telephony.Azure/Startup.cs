using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telephony.Azure;

/// <summary>
/// Keeps the call recordings, encrypted before they reach Azure. The base Telephony feature purges a removed
/// tenant's recordings, so the container is never removed here.
/// </summary>
/// <remarks>
/// The store moves to Azure Blob Storage when the <c>CrestApps:Telephony:AzureRecordings</c> shell configuration section names a connection
/// string and a container; otherwise the local store of the parent feature stays in use.
/// </remarks>
public sealed class Startup : StartupBase
{
    /// <summary>
    /// The shell configuration section the Azure Blob Storage options are read from.
    /// </summary>
    internal const string ConfigurationSectionName = "CrestApps:Telephony:AzureRecordings";

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddTenantFileStore<IRecordingMediaStore>(
            TelephonyConstants.RecordingMediaFolderName,
            ConfigurationSectionName,
            (serviceProvider, fileStore) => new LocalEncryptedRecordingMediaStore(fileStore, serviceProvider.GetRequiredService<IDataProtectionProvider>()),
            removeOnTenantRemoval: false);
}