using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Shipping.Core.Telemetry;

/// <summary>
/// The meter and activity source of shipping, <c>CrestApps.OrchardCore.Shipping</c>: the calls made to carrier
/// services to quote a parcel and to buy its label. Addresses, parcels, shipment and tracking ids go nowhere on a
/// metric.
/// </summary>
public static class ShippingTelemetry
{
    /// <summary>
    /// The name of the shipping meter and activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Shipping";

    /// <summary>
    /// The <c>operation</c> of quoting a parcel's rates.
    /// </summary>
    public const string OperationQuote = "quote";

    /// <summary>
    /// The <c>operation</c> of buying a parcel's label.
    /// </summary>
    public const string OperationBuyLabel = "buy_label";

    /// <summary>
    /// The carrier service answered, but no carrier quoted the parcel.
    /// </summary>
    public const string OutcomeNoRates = "no_rates";

    /// <summary>
    /// The carrier service is not set up, so it was not called.
    /// </summary>
    public const string OutcomeNotConfigured = "not_configured";

    /// <summary>
    /// The meter of shipping.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Shipping");

    /// <summary>
    /// The activity source of shipping.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("Shipping");

    /// <summary>
    /// The duration of calls to a carrier service, tagged with the <c>provider</c>, the <c>operation</c>
    /// (<see cref="OperationQuote"/> or <see cref="OperationBuyLabel"/>), the <c>outcome</c> and the
    /// <c>status_class</c>. Its count is the number of calls.
    /// </summary>
    public static Histogram<double> CarrierDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.shipping.carrier.duration",
        "The duration of a call to a carrier service to quote a parcel or buy its label.");

    /// <summary>
    /// Starts measuring a call to a carrier service.
    /// </summary>
    /// <param name="provider">The name of the carrier provider, such as <c>EasyPost</c>.</param>
    /// <param name="operation"><see cref="OperationQuote"/> or <see cref="OperationBuyLabel"/>.</param>
    /// <returns>The client scope, which records the <see cref="CarrierDuration"/> when disposed.</returns>
    public static TelemetryScope StartCarrierCall(string provider, string operation)
    {
        return ExternalCallTelemetry.Start(
            ActivitySource,
            "crestapps.shipping.carrier." + operation,
            CarrierDuration,
            provider,
            operation);
    }
}
