using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Recipes;

namespace CrestApps.OrchardCore.MailingLists.Deployments;

/// <summary>
/// Writes every mailing list into a deployment plan's recipe.
/// </summary>
internal sealed class MailingListsDeploymentSource : CatalogDeploymentSource<MailingListsDeploymentStep, MailingList>
{
    public MailingListsDeploymentSource(ICatalogManager<MailingList> manager)
        : base(MailingListsRecipeStep.EntriesName, manager)
    {
    }
}
