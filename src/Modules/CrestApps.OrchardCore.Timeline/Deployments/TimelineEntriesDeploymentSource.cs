using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Timeline.Core.Indexes;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Recipes;
using OrchardCore.Deployment;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Timeline.Deployments;

/// <summary>
/// Writes the TimelineEntries step with the entries of the chosen record kinds, a page at a time, oldest first.
/// </summary>
internal sealed class TimelineEntriesDeploymentSource : DeploymentSourceBase<TimelineEntriesDeploymentStep>
{
    private const int PageSize = 500;

    private readonly ISession _session;

    public TimelineEntriesDeploymentSource(ISession session)
    {
        _session = session;
    }

    protected override async Task ProcessAsync(TimelineEntriesDeploymentStep step, DeploymentPlanResult result)
    {
        var kinds = (step.SubjectKinds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var entries = new JsonArray();

        if (kinds.Length > 0)
        {
            // Every entry of the kinds, a page at a time; the id breaks ties between entries of the same time so no page
            // repeats or misses one.
            for (var skip = 0; ; skip += PageSize)
            {
                var page = (await _session.Query<TimelineEntry, TimelineEntryIndex>(index => index.SubjectKind.IsIn(kinds), collection: TimelineConstants.CollectionName)
                    .OrderBy(index => index.CreatedUtc)
                    .ThenBy(index => index.ItemId)
                    .Skip(skip)
                    .Take(PageSize)
                    .ListAsync()).ToList();

                foreach (var entry in page)
                {
                    entries.Add(JsonSerializer.SerializeToNode(entry, JOptions.Default));

                    // Written out, so the session needn't keep tracking it on a large export.
                    _session.Detach(entry, TimelineConstants.CollectionName);
                }

                if (page.Count < PageSize)
                {
                    break;
                }
            }
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            [TimelineEntriesRecipeStep.EntriesName] = entries,
        });
    }
}
