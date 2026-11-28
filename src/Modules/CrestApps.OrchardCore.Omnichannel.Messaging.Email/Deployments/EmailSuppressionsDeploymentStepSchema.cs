using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deployments;

/// <summary>
/// Describes the email suppression list deployment step to CrestApps Recipes.
/// </summary>
internal sealed class EmailSuppressionsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc/>
    public override string StepType => nameof(EmailSuppressionsDeploymentStep);

    /// <inheritdoc/>
    protected override string DisplayText => "Email Suppression List";

    /// <inheritdoc/>
    protected override string Description => "Exports every suppressed email address with its reason and detail, so another site stops sending to the same addresses.";

    /// <inheritdoc/>
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
