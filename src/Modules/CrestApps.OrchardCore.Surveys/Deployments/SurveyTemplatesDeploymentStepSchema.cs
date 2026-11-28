using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Surveys.Deployments;

/// <summary>
/// Describes the survey templates deployment step.
/// </summary>
internal sealed class SurveyTemplatesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => nameof(SurveyTemplatesDeploymentStep);

    /// <inheritdoc />
    protected override string DisplayText => "Survey templates";

    /// <inheritdoc />
    protected override string Description => "Exports the templates new surveys start from.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether every template is exported."));
        yield return ("TemplateNames", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The technical names of the templates when not all are."));
    }
}
