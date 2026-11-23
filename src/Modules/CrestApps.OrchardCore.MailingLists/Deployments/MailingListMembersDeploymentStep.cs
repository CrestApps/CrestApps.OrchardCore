using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.MailingLists.Deployments;

/// <summary>
/// Exports the members of every mailing list with the evidence of their consent. Members are personal data, so the step
/// is never part of a plan unless someone adds it.
/// </summary>
public sealed class MailingListMembersDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<MailingListMembersDeploymentStep>("Mailing Lists");
    private static readonly LocalizationSource _title = LocalizationSource.Create<MailingListMembersDeploymentStep>("Mailing list members");

    /// <inheritdoc/>
    public override string Name => Recipes.MailingListMembersRecipeStep.RecipeName;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}
