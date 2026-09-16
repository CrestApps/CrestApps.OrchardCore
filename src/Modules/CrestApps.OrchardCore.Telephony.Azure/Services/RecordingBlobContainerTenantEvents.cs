using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telephony.Azure.Services;

/// <summary>
/// Ensures the Azure Blob container that backs Contact Center recordings exists when the tenant activates.
/// </summary>
/// <remarks>
/// Only container creation lives here. Removing a tenant's recordings on tenant removal is owned by the base
/// Telephony feature's recording-media purge (the encrypted store implements <c>ISupportsTenantMediaPurge</c>, and
/// the base tenant-removal handler blocks removal until it completes), so this handler never deletes blobs. The
/// container itself is not removed because it is shared across tenants through a per-tenant base path.
/// </remarks>
public sealed class RecordingBlobContainerTenantEvents : ModularTenantEvents
{
    private readonly TelephonyRecordingBlobStorageOptions _options;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingBlobContainerTenantEvents"/> class.
    /// </summary>
    /// <param name="options">The blob storage options.</param>
    /// <param name="shellSettings">The shell settings.</param>
    /// <param name="logger">The logger.</param>
    public RecordingBlobContainerTenantEvents(
        IOptions<TelephonyRecordingBlobStorageOptions> options,
        ShellSettings shellSettings,
        ILogger<RecordingBlobContainerTenantEvents> logger)
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
            _logger.LogDebug("Testing Azure Telephony recording storage container {ContainerName} existence.", _options.ContainerName);
        }

        try
        {
            var blobContainer = new BlobContainerClient(_options.ConnectionString, _options.ContainerName);
            await blobContainer.CreateIfNotExistsAsync(PublicAccessType.None);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Azure Telephony recording storage container {ContainerName} created.", _options.ContainerName);
            }
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Unable to create Azure Telephony recording storage container.");
        }
    }
}
