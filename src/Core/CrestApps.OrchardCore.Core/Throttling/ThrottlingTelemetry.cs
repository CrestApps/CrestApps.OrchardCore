using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Core.Throttling;

/// <summary>
/// Telemetry of the attempt limiter. Refusals are tagged with the policy name only: never the key, an address or an
/// e-mail.
/// </summary>
public static class ThrottlingTelemetry
{
    /// <summary>
    /// The tag that names the attempt policy.
    /// </summary>
    public const string PolicyTag = "crestapps.throttling.policy";

    /// <summary>
    /// The number of attempts an attempt policy refused, tagged with <see cref="PolicyTag"/>.
    /// </summary>
    public static Counter<long> Refusals { get; } = CrestAppsOrchardCoreTelemetry.PlatformMeter.CreateCounter<long>(
        "crestapps.throttling.refusals",
        unit: "{attempt}",
        description: "The attempts an attempt policy refused.");

    /// <summary>
    /// Records one refusal.
    /// </summary>
    /// <param name="policyName">The policy that refused.</param>
    public static void RecordRefusal(string policyName)
        => Refusals.Add(1, new KeyValuePair<string, object>(PolicyTag, policyName));
}
