using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Orders.Workflows;

/// <summary>
/// A workflow event about an order. The input carries the order, its id, number, total and customer email, and what
/// the event adds, such as the amount refunded.
/// </summary>
public abstract class OrderEvent : EventActivity
{
    internal readonly IStringLocalizer S;

    protected OrderEvent(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override LocalizedString Category => S["Commerce"];

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}

public sealed class OrderPaidEvent : OrderEvent
{
    public OrderPaidEvent(IStringLocalizer<OrderPaidEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Name => nameof(OrderPaidEvent);

    public override LocalizedString DisplayText => S["Order Paid"];
}

public sealed class OrderCanceledEvent : OrderEvent
{
    public OrderCanceledEvent(IStringLocalizer<OrderCanceledEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Name => nameof(OrderCanceledEvent);

    public override LocalizedString DisplayText => S["Order Canceled"];
}

public sealed class OrderRefundedEvent : OrderEvent
{
    public OrderRefundedEvent(IStringLocalizer<OrderRefundedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Name => nameof(OrderRefundedEvent);

    public override LocalizedString DisplayText => S["Order Refunded"];
}

/// <summary>
/// The order events have no settings, so they only render their thumbnail and design shapes.
/// </summary>
public sealed class OrderEventDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity>
    where TActivity : OrderEvent
{
}
