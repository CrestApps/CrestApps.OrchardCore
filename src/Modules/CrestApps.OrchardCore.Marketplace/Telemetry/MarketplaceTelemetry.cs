using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Marketplace.Telemetry;

/// <summary>
/// The meter of the marketplace, <c>CrestApps.OrchardCore.Marketplace</c>: the transfers that pay vendors what they
/// earned. Vendors, orders, amounts and accounts go nowhere on a metric.
/// </summary>
public static class MarketplaceTelemetry
{
    /// <summary>
    /// The name of the marketplace meter.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Marketplace";

    /// <summary>
    /// The payout provider made the transfer.
    /// </summary>
    public const string OutcomePaid = "paid";

    /// <summary>
    /// The payout provider refused the transfer; the earnings are tried again with a new key.
    /// </summary>
    public const string OutcomeFailed = "failed";

    /// <summary>
    /// Whether a transfer was made isn't known yet (the provider couldn't be reached, or answered with a temporary or
    /// idempotency error); the payout stays pending with its key.
    /// </summary>
    public const string OutcomeUnsettled = "unsettled";

    /// <summary>
    /// The meter of the marketplace.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Marketplace");

    /// <summary>
    /// The number of transfers that pay vendors, tagged with the <c>provider</c> and the <c>outcome</c>.
    /// </summary>
    public static Counter<long> Payouts { get; } = Meter.CreateCounter<long>(
        "crestapps.marketplace.payouts",
        "{payout}",
        "The number of transfers that pay vendors what they earned, by provider and outcome.");

    /// <summary>
    /// Records a transfer that pays a vendor.
    /// </summary>
    /// <param name="provider">The name of the payout provider.</param>
    /// <param name="outcome">How the transfer went.</param>
    public static void RecordPayout(string provider, string outcome)
    {
        if (!Payouts.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ProviderTag, string.IsNullOrEmpty(provider) ? CrestAppsOrchardCoreTelemetry.Unknown : provider },
            { CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        Payouts.Add(1, tags);
    }
}
