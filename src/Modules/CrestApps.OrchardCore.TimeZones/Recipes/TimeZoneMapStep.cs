using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.TimeZones.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.TimeZones.Recipes;

internal sealed class TimeZoneMapStep : NamedRecipeStepHandler
{
    private readonly INamedCatalogManager<TimeZoneMap> _manager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneMapStep"/> class.
    /// </summary>
    /// <param name="manager">The time zone map manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeZoneMapStep(
        INamedCatalogManager<TimeZoneMap> manager,
        IStringLocalizer<TimeZoneMapStep> stringLocalizer)
        : base(TimeZonesConstants.Recipes.TimeZoneMaps)
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<TimeZoneMapsStepModel>();
        var tokens = model.Maps?.Cast<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            TimeZoneMap map = null;
            var isNew = false;

            var id = token[nameof(TimeZoneMap.ItemId)]?.GetValue<string>();
            var hasId = !string.IsNullOrEmpty(id);

            if (hasId)
            {
                map = await _manager.FindByIdAsync(id);
            }

            if (map is null)
            {
                var name = token[nameof(TimeZoneMap.Name)]?.GetValue<string>()?.Trim();

                if (!string.IsNullOrEmpty(name))
                {
                    map = await _manager.FindByNameAsync(name);
                }
            }

            if (map is not null)
            {
                await _manager.UpdateAsync(map, token);
            }
            else
            {
                isNew = true;
                map = await _manager.NewAsync(token);

                if (hasId && UniqueId.IsValid(id))
                {
                    map.ItemId = id;
                }
            }

            var validationResult = await _manager.ValidateAsync(map);

            if (!validationResult.Succeeded)
            {
                foreach (var error in validationResult.Errors)
                {
                    context.Errors.Add(error.ErrorMessage);
                }

                continue;
            }

            if (isNew)
            {
                await _manager.CreateAsync(map);
            }
        }
    }

    private sealed class TimeZoneMapsStepModel
    {
        /// <summary>
        /// Gets or sets the collection of time zone maps to import.
        /// </summary>
        public JsonArray Maps { get; set; }
    }
}
