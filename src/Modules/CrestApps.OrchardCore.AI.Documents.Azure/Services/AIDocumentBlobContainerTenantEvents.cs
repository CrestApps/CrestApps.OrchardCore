using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Removing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Documents.Azure.Services;

public sealed class AIDocumentBlobContainerTenantEvents : ModularTenantEvents
{
    private readonly AIDocumentBlobStorageOptions _options;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public AIDocumentBlobContainerTenantEvents(
        IOptions<AIDocumentBlobStorageOptions> options,
        ShellSettings shellSettings,
        IStringLocalizer<AIDocumentBlobContainerTenantEvents> localizer,
        ILogger<AIDocumentBlobContainerTenantEvents> logger)
    {
        _options = options.Value;
        _shellSettings = shellSettings;
        S = localizer;
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
            _logger.LogDebug("Testing Azure AI document storage container {ContainerName} existence", _options.ContainerName);
        }

        try
        {
            var blobContainer = new BlobContainerClient(_options.ConnectionString, _options.ContainerName);
            await blobContainer.CreateIfNotExistsAsync(PublicAccessType.None);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Azure AI document storage container {ContainerName} created.", _options.ContainerName);
            }
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Unable to create Azure AI document storage container.");
        }
    }

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
                    _logger.LogError("Unable to remove the Azure AI document storage container {ContainerName}.", _options.ContainerName);

                    context.ErrorMessage = S["Unable to remove the Azure AI document storage container '{0}'.", _options.ContainerName];
                }
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Failed to remove the Azure AI document storage container {ContainerName}.", _options.ContainerName);

                context.ErrorMessage = S["Failed to remove the Azure AI document storage container '{0}'.", _options.ContainerName];
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
                        _logger.LogError("AI document blob removal failed for file {ItemName}.", blobItem.Name);

                        context.ErrorMessage = S["AI document blob removal failed for file {0}.", blobItem.Name];
                        break;
                    }
                }
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Error during Azure AI document blob item removal.");

                context.ErrorMessage = S["Error during Azure AI document blob item removal."];
                context.Error = ex;
            }
        }
    }
}
