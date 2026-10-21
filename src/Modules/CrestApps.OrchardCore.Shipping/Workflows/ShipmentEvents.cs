using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Shipping.Workflows;

/// <summary>
/// A workflow event about a shipment. The input carries the order, the shipment, and the shipment's number, carrier,
/// tracking number and link.
/// </summary>
public abstract class ShipmentEvent : EventActivity
{
    internal readonly IStringLocalizer S;

    protected ShipmentEvent(IStringLocalizer stringLocalizer)
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

public sealed class ShipmentCreatedEvent : ShipmentEvent
{
    public ShipmentCreatedEvent(IStringLocalizer<ShipmentCreatedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Name => nameof(ShipmentCreatedEvent);

    public override LocalizedString DisplayText => S["Shipment Created"];
}

public sealed class ShipmentShippedEvent : ShipmentEvent
{
    public ShipmentShippedEvent(IStringLocalizer<ShipmentShippedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Name => nameof(ShipmentShippedEvent);

    public override LocalizedString DisplayText => S["Shipment Shipped"];
}

public sealed class ShipmentDeliveredEvent : ShipmentEvent
{
    public ShipmentDeliveredEvent(IStringLocalizer<ShipmentDeliveredEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Name => nameof(ShipmentDeliveredEvent);

    public override LocalizedString DisplayText => S["Shipment Delivered"];
}

public sealed class ShipmentReturnedEvent : ShipmentEvent
{
    public ShipmentReturnedEvent(IStringLocalizer<ShipmentReturnedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    public override string Name => nameof(ShipmentReturnedEvent);

    public override LocalizedString DisplayText => S["Shipment Returned"];
}

/// <summary>
/// The shipment events have no settings, so they only render their thumbnail and design shapes.
/// </summary>
public sealed class ShipmentEventDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity>
    where TActivity : ShipmentEvent
{
}
