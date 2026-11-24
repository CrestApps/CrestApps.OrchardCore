using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Recipes;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deployments;

/// <summary>
/// Exports the email suppression list as an <c>EmailSuppressions</c> recipe step, so another site stops sending to the
/// same addresses. The addresses are personal data.
/// </summary>
public sealed class EmailSuppressionsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<EmailSuppressionsDeploymentStep>("Omnichannel");
    private static readonly LocalizationSource _title = LocalizationSource.Create<EmailSuppressionsDeploymentStep>("Email Suppression List");

    /// <inheritdoc/>
    public override string Name => EmailSuppressionsStep.StepKey;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
