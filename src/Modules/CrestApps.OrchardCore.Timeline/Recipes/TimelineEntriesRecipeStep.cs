using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Timeline.Core.Services;
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

    private readonly TimelineEntryStore _store;

    public TimelineEntriesRecipeStep(TimelineEntryStore store)
        : base(TimelineConstants.RecipeStepName)
    {
        _store = store;
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

            if (entry is null || entry.Subject.IsEmpty || string.IsNullOrWhiteSpace(entry.Kind))
            {
                context.Errors.Add("A timeline entry in the TimelineEntries step has no record or no kind.");

                continue;
            }

            if (!string.IsNullOrEmpty(entry.ItemId) && await _store.FindByIdAsync(entry.ItemId) is not null)
            {
                continue;
            }

            entry.ItemId = string.IsNullOrEmpty(entry.ItemId) ? UniqueId.GenerateId() : entry.ItemId;

            await _store.CreateAsync(entry);
        }
    }
}
