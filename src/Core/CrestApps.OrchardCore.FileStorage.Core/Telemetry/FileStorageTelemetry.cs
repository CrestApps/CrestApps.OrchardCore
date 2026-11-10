using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.FileStorage.Telemetry;

/// <summary>
/// The meter of the tenant file stores, <c>CrestApps.OrchardCore.FileStorage</c>: the Azure Blob containers created
/// when a tenant starts and removed with it. Container names, blob names and tenant names never go on a metric.
/// </summary>
public static class FileStorageTelemetry
{
    /// <summary>
    /// The name of the file storage meter.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".FileStorage";

    /// <summary>
    /// The tag that records the purpose of the store, the folder it was registered with, such as <c>Recordings</c>.
    /// </summary>
    public const string PurposeTag = "purpose";

    /// <summary>
    /// Making sure the store's container exists when the tenant starts.
    /// </summary>
    public const string OperationCreateContainer = "create_container";

    /// <summary>
    /// Removing the store's container with the tenant.
    /// </summary>
    public const string OperationRemoveContainer = "remove_container";

    /// <summary>
    /// Removing the blobs under the store's base path with the tenant.
    /// </summary>
    public const string OperationRemoveBlobs = "remove_blobs";

    /// <summary>
    /// The meter of the tenant file stores.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("FileStorage");

    /// <summary>
    /// The number of container operations run on tenant events, tagged with the
    /// <see cref="CrestAppsOrchardCoreTelemetry.OperationTag"/>, <see cref="PurposeTag"/> and the
    /// <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/>: <c>ok</c> or <c>error</c>. A failure leaves a container
    /// missing for a running tenant, or files behind for a removed one.
    /// </summary>
    public static Counter<long> ContainerOperations { get; } = Meter.CreateCounter<long>(
        "crestapps.filestorage.container.operations",
        "{operation}",
        "The number of Azure Blob container operations run when tenants start or are removed, by outcome.");

    /// <summary>
    /// Records a container operation.
    /// </summary>
    /// <param name="operation">The operation, such as <see cref="OperationCreateContainer"/>.</param>
    /// <param name="purpose">The purpose of the store.</param>
    /// <param name="succeeded">Whether the operation succeeded.</param>
    public static void RecordContainerOperation(string operation, string purpose, bool succeeded)
    {
        if (!ContainerOperations.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.OperationTag, operation },
            { PurposeTag, purpose ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { CrestAppsOrchardCoreTelemetry.OutcomeTag, succeeded ? CrestAppsOrchardCoreTelemetry.OutcomeOk : CrestAppsOrchardCoreTelemetry.OutcomeError },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        ContainerOperations.Add(1, tags);
    }
}
