using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.TenantHierarchy.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.Core.Telemetry;

/// <summary>
/// The meter and activity source of the tenant hierarchy, <c>CrestApps.OrchardCore.TenantHierarchy</c>: the
/// operations a parent tenant runs on its child tenants. Neither the parent's nor the child's tenant name is ever a
/// tag, on a metric or a span; spans carry the child's registry entry id.
/// </summary>
public static class TenantHierarchyTelemetry
{
    /// <summary>
    /// The name of the tenant hierarchy meter and activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".TenantHierarchy";

    /// <summary>
    /// The span tag that carries the registry entry id of the child tenant. It is never a metric tag.
    /// </summary>
    public const string EntryIdTag = "crestapps.tenanthierarchy.entry.id";

    /// <summary>
    /// The <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/> of an operation that returned a failure, such as a
    /// child tenant in the wrong state or a setup that reported errors.
    /// </summary>
    public const string OutcomeFailed = "failed";

    /// <summary>
    /// Creating the shell settings of a new child tenant.
    /// </summary>
    public const string OperationCreate = "create";

    /// <summary>
    /// Running the setup of a new child tenant.
    /// </summary>
    public const string OperationSetup = "setup";

    /// <summary>
    /// Changing the name or the address of a child tenant.
    /// </summary>
    public const string OperationUpdate = "update";

    /// <summary>
    /// Suspending a child tenant.
    /// </summary>
    public const string OperationSuspend = "suspend";

    /// <summary>
    /// Resuming a suspended child tenant.
    /// </summary>
    public const string OperationResume = "resume";

    /// <summary>
    /// Reloading a child tenant.
    /// </summary>
    public const string OperationReload = "reload";

    /// <summary>
    /// Removing a child tenant and its database.
    /// </summary>
    public const string OperationRemove = "remove";

    /// <summary>
    /// Enabling or disabling features in a child tenant.
    /// </summary>
    public const string OperationSetFeatures = "set_features";

    /// <summary>
    /// The meter of the tenant hierarchy.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("TenantHierarchy");

    /// <summary>
    /// The activity source of the tenant hierarchy.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("TenantHierarchy");

    /// <summary>
    /// The duration of an operation on a child tenant, tagged with the <see cref="CrestAppsOrchardCoreTelemetry.OperationTag"/>
    /// and the <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/>: <c>ok</c>, <see cref="OutcomeFailed"/>,
    /// <c>error</c> or <c>cancelled</c>. Its count is the number of operations.
    /// </summary>
    public static Histogram<double> ChildOperationDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.tenanthierarchy.child.operation.duration",
        "The duration of an operation a parent tenant ran on a child tenant.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// Runs an operation on a child tenant and measures it: a <c>crestapps.tenanthierarchy.child.&lt;operation&gt;</c>
    /// span and <see cref="ChildOperationDuration"/>. An operation that returns a failure is recorded as failed.
    /// </summary>
    /// <param name="operation">The operation, such as <see cref="OperationSetup"/>.</param>
    /// <param name="entryId">The registry entry id of the child tenant.</param>
    /// <param name="work">The operation.</param>
    /// <returns>The result of the operation. Exceptions are recorded and rethrown.</returns>
    public static async Task<TenantHierarchyResult> MeasureChildOperationAsync(
        string operation,
        string entryId,
        Func<Task<TenantHierarchyResult>> work)
    {
        ArgumentException.ThrowIfNullOrEmpty(operation);
        ArgumentNullException.ThrowIfNull(work);

        using var telemetry = TelemetryScope.Start(
            ActivitySource,
            "crestapps.tenanthierarchy.child." + operation,
            ChildOperationDuration,
            new TagList
            {
                { CrestAppsOrchardCoreTelemetry.OperationTag, operation },
            });

        telemetry.SetSpanTag(EntryIdTag, entryId);

        try
        {
            var result = await work();

            if (result is null || result.Succeeded)
            {
                telemetry.Succeed();
            }
            else
            {
                telemetry.SetOutcome(OutcomeFailed);
                telemetry.Activity?.SetStatus(ActivityStatusCode.Error, result.Error);
            }

            return result;
        }
        catch (Exception ex)
        {
            telemetry.Fail(ex);

            throw;
        }
    }
}
