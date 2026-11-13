using CrestApps.OrchardCore.Attachments;
using CrestApps.OrchardCore.Attachments.Core.Services;
using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the services of record attachments.
/// </summary>
public static class AttachmentsCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the attachment service, its store, the encrypted tenant file store (in Azure Blob Storage when the
    /// <c>CrestApps:Attachments:AzureBlob</c> section names a container) and the access service.
    /// </summary>
    /// <param name="services">The tenant's services.</param>
    /// <param name="configuration">The shell configuration, for the upload policy.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddAttachmentsCore(this IServiceCollection services, IConfiguration configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configuration is not null)
        {
            services.Configure<AttachmentOptions>(configuration.GetSection(AttachmentsConstants.ConfigurationSection));
        }
        else
        {
            services.AddOptions<AttachmentOptions>();
        }

        services.TryAddScoped<AttachmentStore>();
        services.AddSignedLinks();
        services.TryAddScoped<IAttachmentService, DefaultAttachmentService>();
        services.TryAddScoped<IAttachmentAccessService, DefaultAttachmentAccessService>();
        services.AddTenantFileStore<IAttachmentFileStore>(
            AttachmentsConstants.FolderName,
            AttachmentsConstants.BlobStorageConfigurationSection,
            (serviceProvider, fileStore) => new EncryptedAttachmentFileStore(fileStore, serviceProvider.GetRequiredService<IDataProtectionProvider>()),
            removeOnTenantRemoval: false);

        return services;
    }
}
