using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the schema of the <c>SecretsDeploymentStep</c> of the Orchard Core Secrets module.
/// </summary>
public sealed class SecretsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "SecretsDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Secrets";

    /// <inheritdoc />
    protected override string Description => "Exports the secrets. Without an encryption key, only their metadata is exported, and the importing site reads the values from its configuration.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("EncryptionKeyName", DeploymentSchemaBuilders.String("The name of the RsaKeySecret or X509Secret that holds the public key of the destination. When set, the secret values are exported encrypted with it; otherwise only the metadata is exported."));
    }
}
