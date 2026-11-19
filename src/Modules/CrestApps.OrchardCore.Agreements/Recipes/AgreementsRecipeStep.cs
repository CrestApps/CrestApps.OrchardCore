using CrestApps.Core.Services;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Core.Recipes;

namespace CrestApps.OrchardCore.Agreements.Recipes;

/// <summary>
/// Creates or updates agreements, matched by <c>ItemId</c>.
/// </summary>
internal sealed class AgreementsRecipeStep : CatalogRecipeStep<Agreement>
{
    public const string EntriesName = "Agreements";

    public AgreementsRecipeStep(ICatalogManager<Agreement> manager)
        : base(AgreementsConstants.RecipeStepName, EntriesName, manager)
    {
    }
}
