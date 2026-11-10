using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Marketplace.Telemetry;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Telemetry;
using CrestApps.OrchardCore.Products.Core.Telemetry;
using CrestApps.OrchardCore.Shipping.Core.Telemetry;
using CrestApps.OrchardCore.Subscriptions.Core.Telemetry;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Taxation.Core.Telemetry;

namespace CrestApps.OrchardCore.Tests.Telemetry.Commerce;

/// <summary>
/// The commerce areas (orders, subscriptions, taxation, shipping, products and the marketplace) emit the instruments the
/// observability docs list, under one meter per area, with bounded tags only. The names are a public contract that
/// dashboards and alerts depend on.
/// </summary>
public sealed class CommerceTelemetryContractTests
{
    [Fact]
    public void Orders_RecordCreatedAndCancelledWithBoundedTags()
    {
        // Arrange
        using var capture = new TelemetryCapture();

        // Act
        OrdersTelemetry.RecordCreated("a-channel-nobody-registered", unavailable: true);
        OrdersTelemetry.RecordCancelled(OrderCancelReason.Abandoned);

        // Assert
        Assert.Contains(
            capture.Find("crestapps.orders.created", CrestAppsOrchardCoreTelemetry.ChannelTag, CrestAppsOrchardCoreTelemetry.Other),
            measurement => Equals(measurement.Tags.GetValueOrDefault(CrestAppsOrchardCoreTelemetry.ResultTag), OrdersTelemetry.ResultUnavailable));
        Assert.NotEmpty(capture.Find("crestapps.orders.cancelled", CrestAppsOrchardCoreTelemetry.ReasonTag, "abandoned"));
        AssertNoBannedTags(capture, OrdersTelemetry.SourceName);
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

    [Fact]
    public void Subscriptions_RecordTransitionsWithTheirStatusesOnlyWhenChanged()
    {
        // Arrange
        using var capture = new TelemetryCapture();

        // Act
        SubscriptionsTelemetry.RecordTransition(SubscriptionsTelemetry.OperationRenew, SubscriptionsTelemetry.ResultChanged, SubscriptionStatus.PastDue, SubscriptionStatus.Active);
        SubscriptionsTelemetry.RecordTransition(SubscriptionsTelemetry.OperationRenew, SubscriptionsTelemetry.ResultNoChange);

        // Assert
        var renewals = capture.Find("crestapps.subscriptions.transitions", CrestAppsOrchardCoreTelemetry.OperationTag, SubscriptionsTelemetry.OperationRenew);

        Assert.Contains(renewals, measurement =>
            Equals(measurement.Tags.GetValueOrDefault(CrestAppsOrchardCoreTelemetry.ResultTag), SubscriptionsTelemetry.ResultChanged) &&
            Equals(measurement.Tags.GetValueOrDefault(SubscriptionsTelemetry.FromStatusTag), "past_due") &&
            Equals(measurement.Tags.GetValueOrDefault(SubscriptionsTelemetry.ToStatusTag), "active"));
        Assert.Contains(renewals, measurement =>
            Equals(measurement.Tags.GetValueOrDefault(CrestAppsOrchardCoreTelemetry.ResultTag), SubscriptionsTelemetry.ResultNoChange) &&
            !measurement.Tags.ContainsKey(SubscriptionsTelemetry.FromStatusTag));
        AssertNoBannedTags(capture, SubscriptionsTelemetry.SourceName);
    }

    [Fact]
    public void Subscriptions_ProvisioningRecordsItsDurationAndChildSteps_WithTheJobIdOnTheSpanOnly()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var jobId = "job-" + Guid.NewGuid().ToString("n");

        // Act
        using (var telemetry = SubscriptionsTelemetry.StartProvisioning(jobId))
        {
            using (SubscriptionsTelemetry.StartProvisioningStep("setup"))
            {
            }

            telemetry.AddMetricTag(SubscriptionsTelemetry.AttemptTag, 1);
            telemetry.SetOutcome(SubscriptionsTelemetry.ProvisioningSucceeded);
        }

        // Assert
        var root = Assert.Single(capture.Spans, span => span.OperationName == "crestapps.subscriptions.provisioning" && Equals(span.GetTagItem(SubscriptionsTelemetry.JobIdTag), jobId));
        Assert.Contains(capture.Spans, span => span.OperationName == "crestapps.subscriptions.provisioning.setup" && span.ParentSpanId == root.SpanId);
        Assert.Contains(
            capture.Find("crestapps.subscriptions.provisioning.duration", CrestAppsOrchardCoreTelemetry.OutcomeTag, SubscriptionsTelemetry.ProvisioningSucceeded),
            measurement => Equals(measurement.Tags.GetValueOrDefault(SubscriptionsTelemetry.AttemptTag), 1));
        Assert.DoesNotContain(capture.Measurements, measurement => measurement.Tags.Values.Any(value => Equals(value, jobId)));
        AssertNoBannedTags(capture, SubscriptionsTelemetry.SourceName);
    }

    [Fact]
    public void Taxation_RecordsTheFallbackByProvider()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var provider = "Provider" + Guid.NewGuid().ToString("n");

        // Act
        using (var telemetry = TaxationTelemetry.StartDetermination(provider, external: true))
        {
            telemetry.Fail(new TimeoutException());
            TaxationTelemetry.RecordFallback(provider, telemetry.Outcome);
        }

        // Assert
        var fallback = Assert.Single(capture.Find("crestapps.taxation.determination.fallbacks", CrestAppsOrchardCoreTelemetry.ProviderTag, provider));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeTimeout, fallback.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);
        Assert.Single(capture.Find("crestapps.taxation.determination.duration", CrestAppsOrchardCoreTelemetry.ProviderTag, provider));
        AssertNoBannedTags(capture, TaxationTelemetry.SourceName);
    }

    [Fact]
    public void Shipping_RecordsCarrierCallsByProviderAndOperation()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var provider = "Carrier" + Guid.NewGuid().ToString("n");

        // Act
        using (var telemetry = ShippingTelemetry.StartCarrierCall(provider, ShippingTelemetry.OperationBuyLabel))
        {
            telemetry.SetStatusCode(422);
        }

        // Assert
        var call = Assert.Single(capture.Find("crestapps.shipping.carrier.duration", CrestAppsOrchardCoreTelemetry.ProviderTag, provider));
        Assert.Equal(ShippingTelemetry.OperationBuyLabel, call.Tags[CrestAppsOrchardCoreTelemetry.OperationTag]);
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeError, call.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);
        Assert.Equal("4xx", call.Tags[CrestAppsOrchardCoreTelemetry.StatusClassTag]);
        Assert.Contains(capture.Spans, span => span.OperationName == "crestapps.shipping.carrier.buy_label" && Equals(span.GetTagItem(CrestAppsOrchardCoreTelemetry.ProviderTag), provider));
        AssertNoBannedTags(capture, ShippingTelemetry.SourceName);
    }

    [Fact]
    public void Marketplace_RecordsPayoutsByProviderAndOutcome()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var provider = "Payouts" + Guid.NewGuid().ToString("n");

        // Act
        using (var telemetry = MarketplaceTelemetry.StartPayout(provider, "vendor-1"))
        {
            telemetry.SetOutcome(MarketplaceTelemetry.OutcomeFailed);
            MarketplaceTelemetry.RecordPayout(provider, telemetry.Outcome);
        }

        // Assert
        var payout = Assert.Single(capture.Find("crestapps.marketplace.payouts", CrestAppsOrchardCoreTelemetry.ProviderTag, provider));
        Assert.Equal(MarketplaceTelemetry.OutcomeFailed, payout.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);
        Assert.Single(capture.Find("crestapps.marketplace.payout.duration", CrestAppsOrchardCoreTelemetry.ProviderTag, provider));
        Assert.DoesNotContain(capture.Measurements, measurement => measurement.Tags.Values.Any(value => Equals(value, "vendor-1")));
        AssertNoBannedTags(capture, MarketplaceTelemetry.SourceName);
    }

    [Fact]
    public void Products_RecordTheSearchRebuild()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var finishedUtc = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

        // Act
        using (var telemetry = ProductsTelemetry.StartSearchRebuild())
        {
            telemetry.Succeed();
            ProductsTelemetry.RecordSearchRebuilt(1234, new FixedTimeProvider(finishedUtc));
        }

        // Assert
        Assert.Contains(capture.Measurements, measurement => measurement.Instrument == "crestapps.products.search.rebuild.documents" && measurement.Value == 1234);
        Assert.Contains(capture.Measurements, measurement => measurement.Instrument == "crestapps.products.search.last_rebuild" && measurement.Value == finishedUtc.ToUnixTimeSeconds());
        Assert.NotEmpty(capture.Find("crestapps.products.search.rebuild.duration", CrestAppsOrchardCoreTelemetry.OutcomeTag, CrestAppsOrchardCoreTelemetry.OutcomeOk));
        AssertNoBannedTags(capture, ProductsTelemetry.SourceName);
    }

    private static void AssertNoBannedTags(TelemetryCapture capture, string meterName)
    {
        var banned = capture.Measurements
            .Where(measurement => measurement.Meter == meterName)
            .SelectMany(measurement => measurement.Tags.Keys)
            .Where(CrestAppsOrchardCoreTelemetry.BannedMetricTagKeys.Contains)
            .ToList();

        Assert.Empty(banned);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
            => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow()
            => _utcNow;
    }
}
