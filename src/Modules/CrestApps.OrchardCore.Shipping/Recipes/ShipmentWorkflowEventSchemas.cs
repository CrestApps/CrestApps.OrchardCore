using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using CrestApps.OrchardCore.Shipping.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Shipping.Recipes;

/// <summary>
/// Describes the shipment workflow events to CrestApps Recipes. They have no settings and one <c>Done</c> outcome.
/// </summary>
internal abstract class ShipmentEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Commerce";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}

/// <summary>
/// Describes the <c>ShipmentCreatedEvent</c> workflow event.
/// </summary>
internal sealed class ShipmentCreatedEventSchema : ShipmentEventSchemaBase
{
    public override string Name => nameof(ShipmentCreatedEvent);

    protected override string DisplayText => "Shipment Created";

    protected override string Description => "Starts when a shipment is created. The input carries the order, the shipment, and the shipment's number, carrier, tracking number and link.";
}

/// <summary>
/// Describes the <c>ShipmentShippedEvent</c> workflow event.
/// </summary>
internal sealed class ShipmentShippedEventSchema : ShipmentEventSchemaBase
{
    public override string Name => nameof(ShipmentShippedEvent);

    protected override string DisplayText => "Shipment Shipped";

    protected override string Description => "Starts when a shipment is shipped. The input carries the order, the shipment, and the shipment's number, carrier, tracking number and link.";
}

/// <summary>
/// Describes the <c>ShipmentDeliveredEvent</c> workflow event.
/// </summary>
internal sealed class ShipmentDeliveredEventSchema : ShipmentEventSchemaBase
{
    public override string Name => nameof(ShipmentDeliveredEvent);

    protected override string DisplayText => "Shipment Delivered";

    protected override string Description => "Starts when a shipment is delivered. The input carries the order, the shipment, and the shipment's number, carrier, tracking number and link.";
}

/// <summary>
/// Describes the <c>ShipmentReturnedEvent</c> workflow event.
/// </summary>
internal sealed class ShipmentReturnedEventSchema : ShipmentEventSchemaBase
{
    public override string Name => nameof(ShipmentReturnedEvent);

    protected override string DisplayText => "Shipment Returned";

    protected override string Description => "Starts when a shipment is returned. The input carries the order, the shipment, and the shipment's number, carrier, tracking number and link.";
}
