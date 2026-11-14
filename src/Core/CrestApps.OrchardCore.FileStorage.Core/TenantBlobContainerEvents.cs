using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Removing;
using OrchardCore.FileStorage.AzureBlob;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.FileStorage;

/// <summary>
/// Creates the Azure Blob container of the tenant file store registered for <typeparamref name="TStore"/> when the
/// tenant starts, and removes its container or base path when the tenant is removed, as its options say and when the
/// store was registered to allow it.
/// </summary>
/// <typeparam name="TStore">The store.</typeparam>
public sealed class TenantBlobContainerEvents<TStore> : ModularTenantEvents
    where TStore : class
{
    private readonly TenantStoreBlobStorageOptions<TStore> _options;
    private readonly TenantFileStoreRegistration<TStore> _registration;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantBlobContainerEvents{TStore}"/> class.
    /// </summary>
    /// <param name="options">The store's blob storage options.</param>
    /// <param name="registration">The store's registration.</param>
    /// <param name="shellSettings">The shell settings.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="logger">The logger.</param>
    public TenantBlobContainerEvents(
        IOptions<TenantStoreBlobStorageOptions<TStore>> options,
        TenantFileStoreRegistration<TStore> registration,
        ShellSettings shellSettings,
        IStringLocalizer<TenantBlobContainerEvents<TStore>> stringLocalizer,
        ILogger<TenantBlobContainerEvents<TStore>> logger)
    {
        _options = options.Value;
        _registration = registration;
        _shellSettings = shellSettings;
        S = stringLocalizer;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override async Task ActivatingAsync()
    {
        if (_shellSettings.IsUninitialized() || !_options.IsConfigured() || !_options.CreateContainer)
        {
            return;
        }

        try
        {
            var blobContainer = new BlobContainerClient(_options.ConnectionString, _options.ContainerName);
            await blobContainer.CreateIfNotExistsAsync(PublicAccessType.None);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("The Azure Blob container {ContainerName} of the {Purpose} file store exists.", _options.ContainerName, _registration.Purpose);
            }
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Unable to create the Azure Blob container {ContainerName} of the {Purpose} file store.", _options.ContainerName, _registration.Purpose);
        }
    }

    /// <inheritdoc/>
    public override async Task RemovingAsync(ShellRemovingContext context)
    {
        if (!_registration.RemoveOnTenantRemoval || !_options.IsConfigured() || (!_options.RemoveFilesFromBasePath && !_options.RemoveContainer))
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
                    _logger.LogError("Unable to remove the Azure Blob container {ContainerName} of the {Purpose} file store.", _options.ContainerName, _registration.Purpose);
                    context.ErrorMessage = S["Unable to remove the Azure Blob container '{0}'.", _options.ContainerName];
                }
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Failed to remove the Azure Blob container {ContainerName} of the {Purpose} file store.", _options.ContainerName, _registration.Purpose);
                context.ErrorMessage = S["Failed to remove the Azure Blob container '{0}'.", _options.ContainerName];
                context.Error = ex;
            }

            return;
        }

        // The prefix ends with a delimiter, so removing "TenantA" never deletes "TenantAB/..." of another tenant, and a
        // store with no base path deletes nothing rather than the whole container.
        var prefix = TenantBlobPaths.GetRemovalPrefix(_options.BasePath);

        if (prefix is null)
        {
            _logger.LogWarning("The {Purpose} file store has no base path, so the files of the removed tenant were left in the Azure Blob container {ContainerName}.", _registration.Purpose, _options.ContainerName);

            return;
        }

        try
        {
            await foreach (var blobItem in blobContainer.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, CancellationToken.None))
            {
                var response = await blobContainer.DeleteBlobIfExistsAsync(blobItem.Name);

                if (!response.Value)
                {
                    _logger.LogError("Blob removal failed for file {ItemName} of the {Purpose} file store.", blobItem.Name, _registration.Purpose);
                    context.ErrorMessage = S["Blob removal failed for file {0}.", blobItem.Name];

                    break;
                }
            }
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Error while removing the blobs of the {Purpose} file store.", _registration.Purpose);
            context.ErrorMessage = S["Error while removing the Azure Blob files of the tenant."];
            context.Error = ex;
        }
    }
}
