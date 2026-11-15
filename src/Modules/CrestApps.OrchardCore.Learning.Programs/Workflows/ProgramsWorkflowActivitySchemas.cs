using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Learning.Programs.Workflows;

internal abstract class RequiredLearningEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Learning";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("RequirementIds", WorkflowActivitySchemaBuilders.StringArray("The identifiers of the required learning rules that start the workflow. Empty for every rule."));
    }
}

internal sealed class LearningRequiredEventSchema : RequiredLearningEventSchemaBase
{
    public override string Name => "LearningRequiredEvent";

    protected override string DisplayText => "Learning Required";

    protected override string Description => "Starts when learning is required of a person";
}

internal sealed class RequiredLearningDueSoonEventSchema : RequiredLearningEventSchemaBase
{
    public override string Name => "RequiredLearningDueSoonEvent";

    protected override string DisplayText => "Required Learning Due Soon";

    protected override string Description => "Starts when a reminder before the due date of required learning is sent";
}

internal sealed class RequiredLearningOverdueEventSchema : RequiredLearningEventSchemaBase
{
    public override string Name => "RequiredLearningOverdueEvent";

    protected override string DisplayText => "Required Learning Overdue";

    protected override string Description => "Starts when required learning becomes overdue";
}

internal sealed class RequiredLearningExemptionGrantedEventSchema : RequiredLearningEventSchemaBase
{
    public override string Name => "RequiredLearningExemptionGrantedEvent";

    protected override string DisplayText => "Required Learning Exemption Granted";

    protected override string Description => "Starts when a person is exempted from required learning";
}

internal sealed class LearningPathCompletedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "LearningPathCompletedEvent";

    protected override string Category => "Learning";

    protected override string DisplayText => "Learning Path Completed";

    protected override string Description => "Starts when a person completes a learning path";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("PathIds", WorkflowActivitySchemaBuilders.StringArray("The content item ids of the learning paths that start the workflow. Empty for every path."));
    }
}

internal sealed class RequireLearningTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "RequireLearningTask";

    protected override string Category => "Learning";

    protected override string DisplayText => "Require Learning";

    protected override string Description => "Adds a person to the named people of a required learning rule and runs it";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["RequirementId", "UserId"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("RequirementId", WorkflowActivitySchemaBuilders.String("The identifier of the required learning rule."));
        yield return ("UserId", WorkflowActivitySchemaBuilders.String("The user id of the person. A plain string that supports Liquid."));
    }
}

internal sealed class GrantRequiredLearningExemptionTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "GrantRequiredLearningExemptionTask";

    protected override string Category => "Learning";

    protected override string DisplayText => "Grant Required Learning Exemption";

    protected override string Description => "Exempts a person from a required learning rule with a reason";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["RequirementId", "UserId", "Reason"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("RequirementId", WorkflowActivitySchemaBuilders.String("The identifier of the required learning rule."));
        yield return ("UserId", WorkflowActivitySchemaBuilders.String("The user id of the person. A plain string that supports Liquid."));
        yield return ("Reason", WorkflowActivitySchemaBuilders.String("Why the person is exempt. A plain string that supports Liquid."));
    }
}
