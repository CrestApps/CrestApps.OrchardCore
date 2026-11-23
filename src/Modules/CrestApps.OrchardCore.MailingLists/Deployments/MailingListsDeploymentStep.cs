using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.MailingLists.Deployments;

/// <summary>
/// Exports every mailing list, without its members.
/// </summary>
public sealed class MailingListsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<MailingListsDeploymentStep>("Mailing Lists");
    private static readonly LocalizationSource _title = LocalizationSource.Create<MailingListsDeploymentStep>("Mailing lists");

    /// <inheritdoc/>
    public override string Name => Recipes.MailingListsRecipeStep.RecipeName;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}
