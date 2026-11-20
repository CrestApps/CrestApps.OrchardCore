using CrestApps.Core.Services;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Core.Recipes;

namespace CrestApps.OrchardCore.Banking.Checks.Recipes;

/// <summary>
/// Imports check layouts, matched by id and then by name. Signature images and the check line font never travel in a
/// recipe: upload them on the importing site.
/// </summary>
internal sealed class CheckLayoutsRecipeStep : NamedCatalogRecipeStep<CheckLayout>
{
    /// <summary>
    /// The property that holds the layouts.
    /// </summary>
    public const string EntriesName = "Layouts";

    public CheckLayoutsRecipeStep(INamedCatalogManager<CheckLayout> manager)
        : base(ChecksConstants.LayoutsRecipeStepName, EntriesName, manager)
    {
    }

    protected override bool MatchByName => true;
}
