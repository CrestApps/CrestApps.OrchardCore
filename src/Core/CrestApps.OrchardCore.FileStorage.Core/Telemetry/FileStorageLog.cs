using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.FileStorage.Telemetry;

/// <summary>
/// The log messages of the tenant file stores, with event ids 20600 to 20699 of <see cref="CrestAppsEventIds.Startup"/>.
/// </summary>
internal static partial class FileStorageLog
{
    /// <summary>
    /// A store's container was removed with its tenant.
    /// </summary>
    [LoggerMessage(EventId = CrestAppsEventIds.Startup + 600, Level = LogLevel.Information, Message = "Removed the Azure Blob container {ContainerName} of the {Purpose} file store with its tenant.")]
    public static partial void ContainerRemoved(ILogger logger, string containerName, string purpose);

    /// <summary>
    /// The blobs under a store's base path were removed with its tenant.
    /// </summary>
    [LoggerMessage(EventId = CrestAppsEventIds.Startup + 601, Level = LogLevel.Information, Message = "Removed {BlobCount} blobs of the {Purpose} file store from the Azure Blob container {ContainerName} with their tenant.")]
    public static partial void BlobsRemoved(ILogger logger, int blobCount, string purpose, string containerName);
}
