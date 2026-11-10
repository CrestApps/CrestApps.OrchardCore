using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Marketplace.Telemetry;

/// <summary>
/// The meter and activity source of the marketplace, <c>CrestApps.OrchardCore.Marketplace</c>: the transfers that pay
/// vendors what they earned. Vendor ids go on spans only; orders, amounts and accounts go nowhere on a metric.
/// </summary>
public static class MarketplaceTelemetry
{
    /// <summary>
    /// The name of the marketplace meter and activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Marketplace";

    /// <summary>
    /// The span tag that carries the id of the vendor paid. It is never a metric tag.
    /// </summary>
    public const string VendorIdTag = "crestapps.marketplace.vendor.id";

    /// <summary>
    /// The payout provider made the transfer.
    /// </summary>
    public const string OutcomePaid = "paid";

    /// <summary>
    /// The payout provider refused the transfer; the earnings are tried again with a new key.
    /// </summary>
    public const string OutcomeFailed = "failed";

    /// <summary>
    /// The meter of the marketplace.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Marketplace");

    /// <summary>
    /// The activity source of the marketplace.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("Marketplace");

    /// <summary>
    /// The duration of the transfers that pay vendors, tagged with the <c>provider</c> and the <c>outcome</c>:
    /// <see cref="OutcomePaid"/>, <see cref="OutcomeFailed"/> or <c>error</c> when the provider threw.
    /// </summary>
    public static Histogram<double> PayoutDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.marketplace.payout.duration",
        "The duration of a transfer that pays a vendor what it earned on an order.");

    /// <summary>
    /// The number of transfers that pay vendors, tagged with the <c>provider</c> and the <c>outcome</c>.
    /// </summary>
    public static Counter<long> Payouts { get; } = Meter.CreateCounter<long>(
        "crestapps.marketplace.payouts",
        "{payout}",
        "The number of transfers that pay vendors what they earned, by provider and outcome.");

    /// <summary>
    /// Starts measuring a transfer that pays a vendor.
    /// </summary>
    /// <param name="provider">The name of the payout provider.</param>
    /// <param name="vendorId">The id of the vendor, set on the span only.</param>
    /// <returns>The client scope, which records the <see cref="PayoutDuration"/> when disposed.</returns>
    public static TelemetryScope StartPayout(string provider, string vendorId)
    {
        var scope = TelemetryScope.Start(
            ActivitySource,
            "crestapps.marketplace.payout",
            PayoutDuration,
            new TagList
            {
                { CrestAppsOrchardCoreTelemetry.ProviderTag, string.IsNullOrEmpty(provider) ? CrestAppsOrchardCoreTelemetry.Unknown : provider },
            },
            ActivityKind.Client);

        scope.SetSpanTag(VendorIdTag, vendorId);

        return scope;
    }

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
