using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Marketplace.Telemetry;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Telemetry;
using CrestApps.OrchardCore.Products.Core.Telemetry;
using CrestApps.OrchardCore.Subscriptions.Core.Telemetry;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Taxation.Core.Telemetry;

namespace CrestApps.OrchardCore.Tests.Telemetry.Commerce;

/// <summary>
/// The commerce areas (orders, subscriptions, taxation, products and the marketplace) emit the instruments the
/// observability docs list, with bounded tags only. The names are a public contract that dashboards and alerts depend on.
/// </summary>
public sealed class CommerceTelemetryContractTests
{
    [Fact]
    public void CommerceInstruments_KeepTheirNames_AndCarryNoBannedTag()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var jobId = "job-" + Guid.NewGuid().ToString("n");

        // Act
        OrdersTelemetry.RecordCreated("a-channel-nobody-registered", unavailable: true);
        OrdersTelemetry.RecordCancelled(OrderCancelReason.Abandoned);
        SubscriptionsTelemetry.RecordTransition(SubscriptionsTelemetry.OperationRenew, SubscriptionsTelemetry.ResultChanged, SubscriptionStatus.PastDue, SubscriptionStatus.Active);

        using (var telemetry = SubscriptionsTelemetry.StartProvisioning(jobId))
        {
            telemetry.SetOutcome(SubscriptionsTelemetry.ProvisioningSucceeded);
        }

        TaxationTelemetry.RecordFallback("a-tax-provider", CrestAppsOrchardCoreTelemetry.OutcomeTimeout);
        MarketplaceTelemetry.RecordPayout("a-payout-provider", MarketplaceTelemetry.OutcomeFailed);

        using (var telemetry = ProductsTelemetry.StartSearchRebuild())
        {
            telemetry.Succeed();
            ProductsTelemetry.RecordSearchRebuilt(1234, TimeProvider.System);
        }

        // Assert
        var names = capture.Measurements.Select(measurement => measurement.Instrument).ToHashSet(StringComparer.Ordinal);

        Assert.Superset(
            new HashSet<string>(StringComparer.Ordinal)
            {
                "crestapps.orders.created",
                "crestapps.orders.cancelled",
                "crestapps.subscriptions.transitions",
                "crestapps.subscriptions.provisioning.duration",
                "crestapps.taxation.determination.fallbacks",
                "crestapps.marketplace.payouts",
                "crestapps.products.search.rebuild.duration",
                "crestapps.products.search.rebuild.documents",
                "crestapps.products.search.last_rebuild",
            },
            names);
        Assert.Contains(capture.Find("crestapps.orders.created", CrestAppsOrchardCoreTelemetry.ChannelTag, CrestAppsOrchardCoreTelemetry.Other), _ => true);
        Assert.DoesNotContain(capture.Measurements, measurement => measurement.Tags.Values.Any(value => Equals(value, jobId)));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public void Orders_MapEveryCancelReasonAndChannelToABoundedValue()
    {
        // Act
        var reasons = Enum.GetValues<OrderCancelReason>().Select(OrdersTelemetry.GetReasonTag).ToList();

        // Assert
        Assert.DoesNotContain(CrestAppsOrchardCoreTelemetry.Other, reasons);
        Assert.Equal(reasons.Count, reasons.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(OrderChannels.PointOfSale, OrdersTelemetry.GetChannelTag(OrderChannels.PointOfSale));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.Other, OrdersTelemetry.GetChannelTag(null));
    }
}
