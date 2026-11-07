using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.TimeZones.Models;

namespace CrestApps.OrchardCore.TimeZones.Recipes;

/// <summary>
/// Imports the time zone maps carried by a recipe step. An entry is matched by its identifier, and then by its name.
/// </summary>
internal sealed class TimeZoneMapStep : NamedCatalogRecipeStep<TimeZoneMap>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneMapStep"/> class.
    /// </summary>
    /// <param name="manager">The time zone map manager.</param>
    public TimeZoneMapStep(INamedCatalogManager<TimeZoneMap> manager)
        : base(TimeZonesConstants.Recipes.TimeZoneMaps, "Maps", manager)
    {
    }

    /// <inheritdoc/>
    protected override bool MatchByName => true;
}