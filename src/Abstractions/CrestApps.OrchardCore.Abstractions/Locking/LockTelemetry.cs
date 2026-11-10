using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Locking;

/// <summary>
/// Measures distributed lock acquisitions: <c>crestapps.lock.acquisitions</c> by result and
/// <c>crestapps.lock.wait.duration</c>, tagged with the <c>lock.area</c> the caller names. A lock key holds record
/// ids, so it is never a tag.
/// </summary>
public static class LockTelemetry
{
    /// <summary>
    /// The tag that records the area that took the lock, such as <c>contactcenter.reservation</c>.
    /// </summary>
    public const string AreaTag = "lock.area";

    /// <summary>
    /// The tag that records how the lock was taken: <see cref="KindNoWait"/>, <see cref="KindWait"/> or <see cref="KindMulti"/>.
    /// </summary>
    public const string KindTag = "lock.kind";

    /// <summary>
    /// The lock was tried once, without waiting for a holder.
    /// </summary>
    public const string KindNoWait = "no_wait";

    /// <summary>
    /// The lock was waited for up to a timeout.
    /// </summary>
    public const string KindWait = "wait";

    /// <summary>
    /// Several locks were taken, all or nothing.
    /// </summary>
    public const string KindMulti = "multi";

    /// <summary>
    /// The lock was acquired.
    /// </summary>
    public const string ResultAcquired = "acquired";

    /// <summary>
    /// A no-wait attempt found the lock held by someone else.
    /// </summary>
    public const string ResultContended = "contended";

    /// <summary>
    /// The lock was still held by someone else when the timeout passed.
    /// </summary>
    public const string ResultTimeout = "timeout";

    /// <summary>
    /// Taking the lock threw, such as when the lock store could not be reached.
    /// </summary>
    public const string ResultError = "error";

    /// <summary>
    /// The area of a lock taken by a caller that did not name one.
    /// </summary>
    public const string UnspecifiedArea = "unspecified";

    /// <summary>
    /// The number of lock acquisitions, tagged with <see cref="AreaTag"/>, <see cref="KindTag"/> and the
    /// <see cref="CrestAppsOrchardCoreTelemetry.ResultTag"/>.
    /// </summary>
    public static Counter<long> Acquisitions { get; } = CrestAppsOrchardCoreTelemetry.PlatformMeter.CreateCounter<long>(
        "crestapps.lock.acquisitions",
        "{acquisition}",
        "The number of distributed lock acquisitions, by result.");

    /// <summary>
    /// The time spent acquiring a lock, tagged as <see cref="Acquisitions"/> is.
    /// </summary>
    public static Histogram<double> WaitDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        CrestAppsOrchardCoreTelemetry.PlatformMeter,
        "crestapps.lock.wait.duration",
        "The time spent acquiring a distributed lock.");

    /// <summary>
    /// Records one lock acquisition.
    /// </summary>
    /// <param name="area">The area that took the lock, from a bounded set, or <see langword="null"/>.</param>
    /// <param name="kind">How the lock was taken: <see cref="KindNoWait"/>, <see cref="KindWait"/> or <see cref="KindMulti"/>.</param>
    /// <param name="result">The result: <see cref="ResultAcquired"/>, <see cref="ResultContended"/>,
    /// <see cref="ResultTimeout"/> or <see cref="ResultError"/>.</param>
    /// <param name="startTimestamp">The <see cref="Stopwatch"/> timestamp taken before the lock was asked for.</param>
    public static void Record(string area, string kind, string result, long startTimestamp)
    {
        if (!Acquisitions.Enabled && !WaitDuration.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { AreaTag, string.IsNullOrEmpty(area) ? UnspecifiedArea : area },
            { KindTag, kind },
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        Acquisitions.Add(1, tags);
        WaitDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, tags);
    }
}
