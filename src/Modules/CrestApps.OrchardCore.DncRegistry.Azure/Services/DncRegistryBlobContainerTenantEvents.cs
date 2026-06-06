using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Removing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.DncRegistry.Azure.Services;

/// <summary>
/// Handles tenant lifecycle events for the DNC Registry Azure Blob container.
/// Creates the container on activation and optionally removes it on tenant removal.
/// </summary>
public sealed class DncRegistryBlobContainerTenantEvents : ModularTenantEvents
{
    private readonly DncRegistryBlobStorageOptions _options;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DncRegistryBlobContainerTenantEvents"/> class.
    /// </summary>
    /// <param name="options">The blob storage options.</param>
    /// <param name="shellSettings">The shell settings.</param>
    /// <param name="localizer">The string localizer.</param>
    /// <param name="logger">The logger.</param>
    public DncRegistryBlobContainerTenantEvents(
        IOptions<DncRegistryBlobStorageOptions> options,
        ShellSettings shellSettings,
        IStringLocalizer<DncRegistryBlobContainerTenantEvents> localizer,
        ILogger<DncRegistryBlobContainerTenantEvents> logger)
    {
        _options = options.Value;
        _shellSettings = shellSettings;
        S = localizer;
        _logger = logger;
    }

    /// <summary>
    /// Creates the Azure Blob container if it does not exist when the tenant is activated.
    /// </summary>
    public override async Task ActivatingAsync()
    {
        if (_shellSettings.IsUninitialized() || !_options.IsConfigured() || !_options.CreateContainer)
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Testing Azure DNC Registry blob container {ContainerName} existence.", _options.ContainerName);
        }

        try
        {
            var blobContainer = new BlobContainerClient(_options.ConnectionString, _options.ContainerName);
            await blobContainer.CreateIfNotExistsAsync(PublicAccessType.None);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Azure DNC Registry blob container {ContainerName} created.", _options.ContainerName);
            }
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Unable to create Azure DNC Registry blob container.");
        }
    }

    /// <summary>
    /// Removes the Azure Blob container or files when the tenant is removed.
    /// </summary>
    /// <param name="context">The shell removing context.</param>
    public override async Task RemovingAsync(ShellRemovingContext context)
    {
        if (!_options.IsConfigured() || (!_options.RemoveFilesFromBasePath && !_options.RemoveContainer))
        {
            return;
        }

        var blobContainer = new BlobContainerClient(_options.ConnectionString, _options.ContainerName);

        if (_options.RemoveContainer)
        {
            try
            {
                var response = await blobContainer.DeleteIfExistsAsync();

                if (!response.Value)
                {
                    _logger.LogError("Unable to remove the Azure DNC Registry blob container {ContainerName}.", _options.ContainerName);
                    context.ErrorMessage = S["Unable to remove the Azure DNC Registry blob container '{0}'.", _options.ContainerName];
                }
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Failed to remove the Azure DNC Registry blob container {ContainerName}.", _options.ContainerName);
                context.ErrorMessage = S["Failed to remove the Azure DNC Registry blob container '{0}'.", _options.ContainerName];
                context.Error = ex;
            }

            return;
        }

        if (_options.RemoveFilesFromBasePath)
        {
            try
            {
                await foreach (var blobItem in blobContainer.GetBlobsAsync(BlobTraits.None, BlobStates.None, _options.BasePath, CancellationToken.None))
                {
                    var response = await blobContainer.DeleteBlobIfExistsAsync(blobItem.Name);

                    if (!response.Value)
                    {
                        _logger.LogError("DNC Registry blob removal failed for file {ItemName}.", blobItem.Name);
                        context.ErrorMessage = S["DNC Registry blob removal failed for file {0}.", blobItem.Name];
                        break;
                    }
                }
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Error during Azure DNC Registry blob item removal.");
                context.ErrorMessage = S["Error during Azure DNC Registry blob item removal."];
                context.Error = ex;
            }
        }
    }
}
