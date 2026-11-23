using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the schema of the <c>MailingListMembersDeploymentStep</c> of the Mailing Lists module.
/// </summary>
public sealed class MailingListMembersDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "MailingListMembersDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Mailing list members";

    /// <inheritdoc />
    protected override string Description => "Exports the members of every mailing list with the evidence of their consent. Members are personal data: add the step to a plan only to move them to a site you trust.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
