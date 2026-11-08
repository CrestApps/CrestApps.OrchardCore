using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.WorkOrders.Recipes.Workflows;

/// <summary>
/// The base of the work order workflow event schemas: every work order event can be limited to some work order types
/// and has one outcome.
/// </summary>
internal abstract class WorkOrderEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected const string Input = "Input: WorkOrderId, Number, TypeId, Status, Priority, Summary, CustomerId, TerritoryId";

    protected override string Category => "Work Orders";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("TypeIds", WorkflowActivitySchemaBuilders.StringArray("The ids of the work order types the event starts for; empty for every type."));
    }
}

/// <summary>
/// Describes the Work Order Created workflow event.
/// </summary>
internal sealed class WorkOrderCreatedEventSchema : WorkOrderEventSchemaBase
{
    public override string Name => WorkOrdersConstants.Events.Created;

    protected override string DisplayText => "Work Order Created";

    protected override string Description => $"Starts when a work order is created. {Input}";
}

/// <summary>
/// Describes the Work Order Status Changed workflow event.
/// </summary>
internal sealed class WorkOrderStatusChangedEventSchema : WorkOrderEventSchemaBase
{
    public override string Name => WorkOrdersConstants.Events.StatusChanged;

    protected override string DisplayText => "Work Order Status Changed";

    protected override string Description => $"Starts when a work order's status changes. {Input}, plus the change's details";
}

/// <summary>
/// Describes the Work Order Service Level Warning workflow event.
/// </summary>
internal sealed class WorkOrderSlaWarningEventSchema : WorkOrderEventSchemaBase
{
    public override string Name => WorkOrdersConstants.Events.SlaWarning;

    protected override string DisplayText => "Work Order Service Level Warning";

    protected override string Description => $"Starts when a work order is close to missing a service level target. {Input}, plus the target's details";
}

/// <summary>
/// Describes the Work Order Service Level Breached workflow event.
/// </summary>
internal sealed class WorkOrderSlaBreachedEventSchema : WorkOrderEventSchemaBase
{
    public override string Name => WorkOrdersConstants.Events.SlaBreached;

    protected override string DisplayText => "Work Order Service Level Breached";

    protected override string Description => $"Starts when a work order misses a service level target. {Input}, plus the target's details";
}

/// <summary>
/// Describes the Work Order Completed workflow event.
/// </summary>
internal sealed class WorkOrderCompletedEventSchema : WorkOrderEventSchemaBase
{
    public override string Name => WorkOrdersConstants.Events.Completed;

    protected override string DisplayText => "Work Order Completed";

    protected override string Description => $"Starts when a work order is completed. {Input}";
}

/// <summary>
/// Describes the Service Agreement Expiring workflow event.
/// </summary>
internal sealed class ServiceAgreementExpiringEventSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => WorkOrdersConstants.Events.AgreementExpiring;

    protected override string Category => "Work Orders";

    protected override string DisplayText => "Service Agreement Expiring";

    protected override string Description => "Starts once when a service agreement comes within its expiring notice days of its end date. Input: AgreementId, Number, Name, EndDate, CustomerId, AccountId";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}
