using CrestApps.OrchardCore.Orders.Workflows;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Orders.Recipes;

/// <summary>
/// Describes the order workflow events to CrestApps Recipes. They have no settings and one <c>Done</c> outcome.
/// </summary>
internal abstract class OrderEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Commerce";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}

/// <summary>
/// Describes the <c>OrderPaidEvent</c> workflow event.
/// </summary>
internal sealed class OrderPaidEventSchema : OrderEventSchemaBase
{
    public override string Name => nameof(OrderPaidEvent);

    protected override string DisplayText => "Order Paid";

    protected override string Description => "Starts when an order is paid. The input carries the order, its id, number, total and customer email.";
}

/// <summary>
/// Describes the <c>OrderCanceledEvent</c> workflow event.
/// </summary>
internal sealed class OrderCanceledEventSchema : OrderEventSchemaBase
{
    public override string Name => nameof(OrderCanceledEvent);

    protected override string DisplayText => "Order Canceled";

    protected override string Description => "Starts when an order is canceled. The input carries the order, its id, number, total and customer email.";
}

/// <summary>
/// Describes the <c>OrderRefundedEvent</c> workflow event.
/// </summary>
internal sealed class OrderRefundedEventSchema : OrderEventSchemaBase
{
    public override string Name => nameof(OrderRefundedEvent);

    protected override string DisplayText => "Order Refunded";

    protected override string Description => "Starts when an order is refunded. The input carries the order, its id, number, total and customer email, and the amount refunded.";
}
