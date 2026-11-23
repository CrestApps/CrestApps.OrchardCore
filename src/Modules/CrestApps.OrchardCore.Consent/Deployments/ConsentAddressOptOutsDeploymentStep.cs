using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Consent.Deployments;

/// <summary>
/// Exports every opt-out for one purpose (such as marketing) recorded against an address, as a
/// <c>ConsentAddressOptOuts</c> recipe step, so a person's "stop all marketing" moves with the site. The opt-outs hold
/// e-mail addresses and phone numbers, which are personal data.
/// </summary>
public sealed class ConsentAddressOptOutsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<ConsentAddressOptOutsDeploymentStep>("Communication");
    private static readonly LocalizationSource _title = LocalizationSource.Create<ConsentAddressOptOutsDeploymentStep>("Address opt-outs");

    /// <inheritdoc/>
    public override string Name => nameof(ConsentAddressOptOutsDeploymentStep);

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}
