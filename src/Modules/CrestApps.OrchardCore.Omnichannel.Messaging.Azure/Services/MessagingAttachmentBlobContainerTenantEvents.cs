using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Azure.Services;

/// <summary>
/// Ensures the Azure Blob container that backs messaging pictures exists when the tenant activates.
/// </summary>
/// <remarks>
/// Only container creation lives here. Pictures are removed with the message that carries them, and the container
/// is not removed because it is shared across tenants through a per-tenant base path.
/// </remarks>
public sealed class MessagingAttachmentBlobContainerTenantEvents : ModularTenantEvents
{
    private readonly MessagingAttachmentBlobStorageOptions _options;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingAttachmentBlobContainerTenantEvents"/> class.
    /// </summary>
    /// <param name="options">The blob storage options.</param>
    /// <param name="shellSettings">The shell settings.</param>
    /// <param name="logger">The logger.</param>
    public MessagingAttachmentBlobContainerTenantEvents(
        IOptions<MessagingAttachmentBlobStorageOptions> options,
        ShellSettings shellSettings,
        ILogger<MessagingAttachmentBlobContainerTenantEvents> logger)
    {
        _options = options.Value;
        _shellSettings = shellSettings;
        _logger = logger;
    }

    public override async Task ActivatingAsync()
    {
        if (_shellSettings.IsUninitialized() || !_options.IsConfigured() || !_options.CreateContainer)
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Testing Azure messaging attachment storage container {ContainerName} existence.", _options.ContainerName);
        }

        try
        {
            var blobContainer = new BlobContainerClient(_options.ConnectionString, _options.ContainerName);
            await blobContainer.CreateIfNotExistsAsync(PublicAccessType.None);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Azure messaging attachment storage container {ContainerName} created.", _options.ContainerName);
            }
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Unable to create Azure messaging attachment storage container.");
        }
    }
}
