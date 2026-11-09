using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Dispatch.Recipes.Workflows;

/// <summary>
/// The base of the visit workflow event schemas.
/// </summary>
internal abstract class VisitEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected const string Input = "Input: VisitId, Number, WorkOrderId, WorkOrderNumber, Status, LeadWorkerId, WorkerIds, StartUtc, EndUtc, ArrivalStartUtc, ArrivalEndUtc";

    protected override string Category => "Dispatch";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}

/// <summary>
/// Describes the Visit Assigned workflow event.
/// </summary>
internal sealed class VisitAssignedEventSchema : VisitEventSchemaBase
{
    public override string Name => DispatchConstants.Events.Assigned;

    protected override string DisplayText => "Visit Assigned";

    protected override string Description => $"Starts when a visit is assigned, reassigned or moved. {Input}";
}

/// <summary>
/// Describes the Visit Status Changed workflow event.
/// </summary>
internal sealed class VisitStatusChangedEventSchema : VisitEventSchemaBase
{
    public override string Name => DispatchConstants.Events.StatusChanged;

    protected override string DisplayText => "Visit Status Changed";

    protected override string Description => $"Starts when a visit's status changes. {Input}";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("Statuses", WorkflowActivitySchemaBuilders.StringEnumArray(
            "The statuses the event starts for; empty for every status.",
            [.. Enum.GetNames<VisitStatus>().Where(status => status is not (nameof(VisitStatus.Unscheduled) or nameof(VisitStatus.Scheduled)))]));
    }
}

/// <summary>
/// Describes the Visit At Risk workflow event.
/// </summary>
internal sealed class VisitAtRiskEventSchema : VisitEventSchemaBase
{
    public override string Name => DispatchConstants.Events.AtRisk;

    protected override string DisplayText => "Visit At Risk";

    protected override string Description => $"Starts when a visit is at risk: running over, late to start, or its technician is off. {Input}";
}
