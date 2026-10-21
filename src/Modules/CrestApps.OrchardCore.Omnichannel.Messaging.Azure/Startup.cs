using CrestApps.OrchardCore.Omnichannel.Messaging.Azure.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.FileStorage.AzureBlob;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Azure;

/// <summary>
/// Swaps the messaging attachment store to an Azure Blob Storage backend when this feature is enabled and
/// configured. The bytes are still encrypted at rest by the same data-protection wrapper the local store uses, so
/// enabling this feature only changes where the encrypted pictures live. When the connection string or container
/// name is missing the feature stays inert and the local store remains in effect.
/// </summary>
public sealed class Startup : StartupBase
{
    private readonly ILogger _logger;
    private readonly IShellConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The shell configuration.</param>
    public Startup(
        ILogger<Startup> logger,
        IShellConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<MessagingAttachmentBlobStorageOptions>, MessagingAttachmentBlobStorageOptionsConfiguration>();

        var section = _configuration.GetSection(MessagingAttachmentBlobStorageOptionsConfiguration.ConfigurationSectionName);
        var connectionString = section.GetValue<string>(nameof(MessagingAttachmentBlobStorageOptions.ConnectionString));
        var containerName = section.GetValue<string>(nameof(MessagingAttachmentBlobStorageOptions.ContainerName));

        if (!CheckOptions(connectionString, containerName, _logger))
        {
            return;
        }

        // Keep the local store's encryption wrapper and swap only its file backend, so pictures are encrypted before
        // they reach Azure.
        services.Replace(ServiceDescriptor.Singleton<IMessagingAttachmentStore>(serviceProvider =>
        {
            var blobStorageOptions = serviceProvider.GetRequiredService<IOptions<MessagingAttachmentBlobStorageOptions>>().Value;
            var clock = serviceProvider.GetRequiredService<IClock>();
            var contentTypeProvider = serviceProvider.GetRequiredService<IContentTypeProvider>();
            var dataProtectionProvider = serviceProvider.GetRequiredService<IDataProtectionProvider>();
            var fileStore = new BlobFileStore(blobStorageOptions, clock, contentTypeProvider);

            return new LocalEncryptedMessagingAttachmentStore(fileStore, dataProtectionProvider);
        }));

        services.AddScoped<IModularTenantEvents, MessagingAttachmentBlobContainerTenantEvents>();
    }

    private static bool CheckOptions(string connectionString, string containerName, ILogger logger)
    {
        var optionsAreValid = true;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogError("Azure messaging attachment storage is enabled but not active because the 'ConnectionString' is missing or empty in application configuration.");
            optionsAreValid = false;
        }

        if (string.IsNullOrWhiteSpace(containerName))
        {
            logger.LogError("Azure messaging attachment storage is enabled but not active because the 'ContainerName' is missing or empty in application configuration.");
            optionsAreValid = false;
        }

        return optionsAreValid;
    }
}
