using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Staff.Workflows;

internal abstract class StaffEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Staff";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}

internal sealed class StaffMemberHiredEventSchema : StaffEventSchemaBase
{
    public override string Name => StaffWorkflowEventNames.Hired;

    protected override string DisplayText => "Staff Member Hired";

    protected override string Description => "Starts when a person starts working. Input: StaffMemberId, UserId, DisplayName, JobTitle, ManagerId, TeamId, Status";
}

internal sealed class StaffMemberLeftEventSchema : StaffEventSchemaBase
{
    public override string Name => StaffWorkflowEventNames.Left;

    protected override string DisplayText => "Staff Member Left";

    protected override string Description => "Starts when a person leaves the business. Input: StaffMemberId, UserId, DisplayName, JobTitle, ManagerId, TeamId, Status";
}

internal sealed class StaffManagerChangedEventSchema : StaffEventSchemaBase
{
    public override string Name => StaffWorkflowEventNames.ManagerChanged;

    protected override string DisplayText => "Staff Manager Changed";

    protected override string Description => "Starts when a person gets a new manager. Input: StaffMemberId, UserId, DisplayName, ManagerId, PreviousManagerId";
}
