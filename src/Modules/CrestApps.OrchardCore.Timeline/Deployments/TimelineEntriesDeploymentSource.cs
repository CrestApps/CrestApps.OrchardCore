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
    private const int MaxEntries = 100_000;

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
            for (var skip = 0; skip < MaxEntries; skip += PageSize)
            {
                var page = (await _session.Query<TimelineEntry, TimelineEntryIndex>(index => index.SubjectKind.IsIn(kinds), collection: TimelineConstants.CollectionName)
                    .OrderBy(index => index.CreatedUtc)
                    .Skip(skip)
                    .Take(PageSize)
                    .ListAsync()).ToList();

                foreach (var entry in page)
                {
                    entries.Add(JsonSerializer.SerializeToNode(entry, JOptions.Default));
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
