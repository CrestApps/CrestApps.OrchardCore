using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Azure;

/// <summary>
/// Keeps the messaging pictures, encrypted before they reach Azure.
/// </summary>
/// <remarks>
/// The store moves to Azure Blob Storage when the <c>CrestApps:Omnichannel:Messaging:AzureAttachments</c> shell configuration section names a connection
/// string and a container; otherwise the local store of the parent feature stays in use.
/// </remarks>
public sealed class Startup : StartupBase
{
    /// <summary>
    /// The shell configuration section the Azure Blob Storage options are read from.
    /// </summary>
    internal const string ConfigurationSectionName = "CrestApps:Omnichannel:Messaging:AzureAttachments";

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddTenantFileStore<IMessagingAttachmentStore>(
            LocalEncryptedMessagingAttachmentStore.FolderName,
            ConfigurationSectionName,
            (serviceProvider, fileStore) => new LocalEncryptedMessagingAttachmentStore(fileStore, serviceProvider.GetRequiredService<IDataProtectionProvider>()),
            removeOnTenantRemoval: false);
}