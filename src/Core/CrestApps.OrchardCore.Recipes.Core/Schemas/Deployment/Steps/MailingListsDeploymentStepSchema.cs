using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the schema of the <c>MailingListsDeploymentStep</c> of the Mailing Lists module.
/// </summary>
public sealed class MailingListsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "MailingListsDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Mailing lists";

    /// <inheritdoc />
    protected override string Description => "Exports every mailing list with its settings, without its members.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
