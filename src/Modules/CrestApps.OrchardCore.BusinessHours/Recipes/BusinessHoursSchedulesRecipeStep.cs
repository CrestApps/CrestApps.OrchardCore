using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.Core.Recipes;

namespace CrestApps.OrchardCore.BusinessHours.Recipes;

/// <summary>
/// Imports the business hours schedules a recipe carries, creating the ones that don't exist and updating the others.
/// A schedule is matched by its id, then by its name, so running a recipe twice never duplicates one.
/// </summary>
internal sealed class BusinessHoursSchedulesRecipeStep : NamedCatalogRecipeStep<BusinessHoursSchedule>
{
    /// <summary>
    /// The name of the step member that holds the schedules.
    /// </summary>
    public const string EntriesName = "Schedules";

    public BusinessHoursSchedulesRecipeStep(INamedCatalogManager<BusinessHoursSchedule> manager)
        : base(BusinessHoursConstants.RecipeStepName, EntriesName, manager)
    {
    }

    protected override bool MatchByName => true;
}
