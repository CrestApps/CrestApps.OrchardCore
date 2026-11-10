using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Orders.Core.Models;

namespace CrestApps.OrchardCore.Orders.Core.Telemetry;

/// <summary>
/// The meter of orders, <c>CrestApps.OrchardCore.Orders</c>: the orders made and the orders canceled, by why. Order
/// ids, numbers, customers and amounts go nowhere on a metric.
/// </summary>
public static class OrdersTelemetry
{
    /// <summary>
    /// The name of the orders meter.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Orders";

    /// <summary>
    /// An order was made and everything it needed was held for it.
    /// </summary>
    public const string ResultCreated = "created";

    /// <summary>
    /// An order was made, but something it needed couldn't be held, so it was canceled at once.
    /// </summary>
    public const string ResultUnavailable = "unavailable";

    /// <summary>
    /// The meter of orders.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Orders");

    /// <summary>
    /// The number of orders made, tagged with the <c>channel</c> (<c>online</c>, <c>pos</c>, <c>draft</c> or
    /// <c>other</c>) and the <c>result</c>: <see cref="ResultCreated"/> or <see cref="ResultUnavailable"/>.
    /// </summary>
    public static Counter<long> Created { get; } = Meter.CreateCounter<long>(
        "crestapps.orders.created",
        "{order}",
        "The number of orders made, by channel and whether what they needed could be held.");

    /// <summary>
    /// The number of orders canceled, tagged with the <c>reason</c>, such as <c>abandoned</c>,
    /// <c>checkout_expired</c> or <c>payment_failed</c>. Abandoned orders are the ones with <c>reason=abandoned</c>.
    /// </summary>
    public static Counter<long> Cancelled { get; } = Meter.CreateCounter<long>(
        "crestapps.orders.cancelled",
        "{order}",
        "The number of orders canceled, by why.");

    /// <summary>
    /// Records an order that was made.
    /// </summary>
    /// <param name="channel">The order's channel, such as <see cref="OrderChannels.Online"/>.</param>
    /// <param name="unavailable">Whether something the order needed couldn't be held for it.</param>
    public static void RecordCreated(string channel, bool unavailable)
    {
        if (!Created.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ChannelTag, GetChannelTag(channel) },
            { CrestAppsOrchardCoreTelemetry.ResultTag, unavailable ? ResultUnavailable : ResultCreated },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        Created.Add(1, tags);
    }

    /// <summary>
    /// Records an order that was canceled.
    /// </summary>
    /// <param name="reason">Why it was canceled.</param>
    public static void RecordCancelled(OrderCancelReason reason)
    {
        if (!Cancelled.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ReasonTag, GetReasonTag(reason) },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        Cancelled.Add(1, tags);
    }

    /// <summary>
    /// Gets the bounded <c>channel</c> tag value of an order's channel.
    /// </summary>
    /// <param name="channel">The order's channel.</param>
    /// <returns>One of the <see cref="OrderChannels"/> values, or <c>other</c>.</returns>
    public static string GetChannelTag(string channel)
    {
        return channel switch
        {
            OrderChannels.Online => OrderChannels.Online,
            OrderChannels.PointOfSale => OrderChannels.PointOfSale,
            OrderChannels.Draft => OrderChannels.Draft,
            _ => CrestAppsOrchardCoreTelemetry.Other,
        };
    }

    /// <summary>
    /// Gets the bounded <c>reason</c> tag value of why an order was canceled.
    /// </summary>
    /// <param name="reason">Why the order was canceled.</param>
    /// <returns>The reason in snake case, such as <c>checkout_expired</c>.</returns>
    public static string GetReasonTag(OrderCancelReason reason)
    {
        return reason switch
        {
            OrderCancelReason.Replaced => "replaced",
            OrderCancelReason.PaymentFailed => "payment_failed",
            OrderCancelReason.CheckoutCanceled => "checkout_cancelled",
            OrderCancelReason.CheckoutExpired => "checkout_expired",
            OrderCancelReason.Abandoned => "abandoned",
            OrderCancelReason.Unavailable => "unavailable",
            OrderCancelReason.CanceledByStore => "cancelled_by_store",
            OrderCancelReason.Declined => "declined",
            OrderCancelReason.Rejected => "rejected",
            _ => CrestAppsOrchardCoreTelemetry.Other,
        };
    }
}
