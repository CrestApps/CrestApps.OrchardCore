using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Learning.Coursework.Workflows;

internal abstract class CourseworkEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Learning";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("CourseworkIds", WorkflowActivitySchemaBuilders.StringArray("The content item ids of the coursework that starts the workflow. Empty for every coursework item."));
    }
}

internal sealed class CourseworkSubmittedEventSchema : CourseworkEventSchemaBase
{
    public override string Name => "CourseworkSubmittedEvent";

    protected override string DisplayText => "Coursework Submitted";

    protected override string Description => "Starts when a learner hands in coursework";
}

internal sealed class CourseworkGradedEventSchema : CourseworkEventSchemaBase
{
    public override string Name => "CourseworkGradedEvent";

    protected override string DisplayText => "Coursework Graded";

    protected override string Description => "Starts when coursework is graded, or graded again";
}

internal sealed class ReturnCourseworkTaskSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => "ReturnCourseworkTask";

    protected override string Category => "Learning";

    protected override string DisplayText => "Return Coursework For Resubmission";

    protected override string Description => "Returns a learner's coursework submission to hand in again, with feedback";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["SubmissionId"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("SubmissionId", WorkflowActivitySchemaBuilders.String("The id of the submission. A plain string that supports Liquid."));
        yield return ("Feedback", WorkflowActivitySchemaBuilders.String("What the learner should change. A plain string that supports Liquid."));
    }
}
