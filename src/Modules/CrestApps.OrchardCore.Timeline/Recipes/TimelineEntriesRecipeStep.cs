using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Timeline.Services;
using CrestApps.OrchardCore.Timeline.Models;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Timeline.Recipes;

/// <summary>
/// Imports timeline entries, such as history brought over from another system. Entries are kept as written, so an
/// entry whose id is already stored is left as it is; the others are added with their own times.
/// </summary>
internal sealed class TimelineEntriesRecipeStep : NamedRecipeStepHandler
{
    /// <summary>
    /// The name of the step member that holds the entries.
    /// </summary>
    public const string EntriesName = "Entries";

    private readonly ITimelineService _timelineService;

    public TimelineEntriesRecipeStep(ITimelineService timelineService)
        : base(TimelineConstants.RecipeStepName)
    {
        _timelineService = timelineService;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step[EntriesName] is not JsonArray entries)
        {
            return;
        }

        foreach (var node in entries.OfType<JsonObject>())
        {
            var entry = node.Deserialize<TimelineEntry>(JOptions.Default);

            if (entry is null)
            {
                context.Errors.Add("A timeline entry in the TimelineEntries step can't be read.");

                continue;
            }

            // The import path checks and stamps the entry like a new one, leaves an entry already stored as it is, and
            // announces nothing.
            var result = await _timelineService.ImportAsync(entry);

            if (!result.Succeeded)
            {
                context.Errors.Add($"A timeline entry in the TimelineEntries step was refused: {string.Join(" ", result.Errors)}");
            }
        }
    }
}
