using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Learning.Assessments.Workflows;

internal abstract class AttemptEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Learning";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("QuizIds", WorkflowActivitySchemaBuilders.StringArray("The content item ids of the quizzes whose attempts start the workflow. Empty for every quiz."));
        yield return ("IncludeWorkflowChanges", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether a change a workflow task made starts the workflow too. False by default."));
    }
}

internal abstract class AttemptTaskSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Learning";

    protected override IEnumerable<string> Outcomes => ["Done", "NotFound", "Failed"];

    protected override IEnumerable<string> RequiredProperties => ["AttemptId", "Reason"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("AttemptId", WorkflowActivitySchemaBuilders.String("The attempt id. A plain string that supports Liquid."));
        yield return ("Reason", WorkflowActivitySchemaBuilders.String("Why. Required; kept in the attempt's score history. A plain string that supports Liquid."));
        yield return ("OutputName", WorkflowActivitySchemaBuilders.String("The workflow output name of the result. Defaults to Attempt."));
    }
}

internal sealed class AttemptStartedEventSchema : AttemptEventSchemaBase
{
    public override string Name => "AttemptStartedEvent";

    protected override string DisplayText => "Quiz Attempt Started";

    protected override string Description => "Starts when a learner starts a quiz attempt";
}

internal sealed class AttemptSubmittedEventSchema : AttemptEventSchemaBase
{
    public override string Name => "AttemptSubmittedEvent";

    protected override string DisplayText => "Quiz Attempt Submitted";

    protected override string Description => "Starts when a quiz attempt is submitted, by the learner or when the time is up";
}

internal sealed class AttemptNeedsGradingEventSchema : AttemptEventSchemaBase
{
    public override string Name => "AttemptNeedsGradingEvent";

    protected override string DisplayText => "Quiz Attempt Needs Grading";

    protected override string Description => "Starts when a submitted quiz attempt has answers a person must grade";
}

internal sealed class AttemptGradedEventSchema : AttemptEventSchemaBase
{
    public override string Name => "AttemptGradedEvent";

    protected override string DisplayText => "Quiz Attempt Graded";

    protected override string Description => "Starts when every answer of a quiz attempt has a score";
}

internal sealed class AttemptScoreChangedEventSchema : AttemptEventSchemaBase
{
    public override string Name => "AttemptScoreChangedEvent";

    protected override string DisplayText => "Quiz Score Changed";

    protected override string Description => "Starts when a graded quiz attempt is overridden, regraded or voided";
}

internal sealed class VoidAttemptTaskSchema : AttemptTaskSchemaBase
{
    public override string Name => "VoidAttemptTask";

    protected override string DisplayText => "Void Quiz Attempt";

    protected override string Description => "Voids a quiz attempt with a reason; it no longer counts toward the quiz result";
}

internal sealed class RegradeAttemptTaskSchema : AttemptTaskSchemaBase
{
    public override string Name => "RegradeAttemptTask";

    protected override string DisplayText => "Regrade Quiz Attempt";

    protected override string Description => "Scores a quiz attempt again with the key it kept, such as after a question was fixed";
}
