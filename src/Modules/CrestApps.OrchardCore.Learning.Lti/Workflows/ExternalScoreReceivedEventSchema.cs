using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Learning.Lti.Workflows;

/// <summary>
/// Describes the External Score Received event to CrestApps Recipes.
/// </summary>
internal sealed class ExternalScoreReceivedEventSchema : WorkflowActivitySchemaDefinitionBase
{
    public override string Name => nameof(ExternalScoreReceivedEvent);

    protected override string Category => "Learning";

    protected override string DisplayText => "External Score Received";

    protected override string Description => "Starts when an external tool (LTI 1.3) posts a score through Assignment and Grade Services";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return ("GradingProgress", WorkflowActivitySchemaBuilders.StringArray("The grading progress values that start the workflow, such as FullyGraded. Empty for all."));
    }
}
