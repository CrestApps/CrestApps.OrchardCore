using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TimeTracker.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using YesSql;

namespace CrestApps.OrchardCore.TimeTracker.AI;

/// <summary>
/// Lists anyone's time entries with the query of the Time Entries page, newest first.
/// </summary>
internal sealed class ListTimeEntriesTool : TimeTrackerToolBase
{
    public const string ToolName = "listTimeEntries";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "userId": { "type": "string", "description": "Optional. The user ID of one person." },
        "userName": { "type": "string", "description": "Optional. Text in the person's name, as the Time Entries search." },
        "stage": { "type": "string", "description": "Optional. A stage's name or ID." },
        "from": { "type": "string", "description": "Optional. The first day (yyyy-MM-dd), in the Time Tracker's time zone; give it with to. Entries still open are not listed when days are given, as on the page." },
        "to": { "type": "string", "description": "Optional. The last day (yyyy-MM-dd), included; give it with from." },
        "page": { "type": "integer", "minimum": 1, "description": "The page, from 1." },
        "pageSize": { "type": "integer", "minimum": 1, "maximum": 50, "description": "Entries per page, 20 by default, at most 50." }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Lists anyone's time entries, newest first, filtered by person, stage and a range of days, with the stage, start, end, note, the clock that keeps an entry and who changed it, when and why.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => TimeTrackerPermissions.ViewTimeEntries;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var zone = await services.GetRequiredService<TimeTrackerTimeZone>().GetAsync();
        var from = TimeTrackerTools.ReadDate(arguments, "from");
        var to = TimeTrackerTools.ReadDate(arguments, "to");

        if ((TimeTrackerTools.Has(arguments, "from") && from is null) || (TimeTrackerTools.Has(arguments, "to") && to is null))
        {
            return "Give the days as yyyy-MM-dd.";
        }

        if ((from is null) != (to is null))
        {
            return "Give both from and to, or neither.";
        }

        if (from > to)
        {
            return "The first day (from) must not be after the last day (to).";
        }

        var stages = await TimeTrackerTools.StagesAsync(services, cancellationToken);
        var stageName = TimeTrackerTools.Read(arguments, "stage");
        string stageId = null;

        if (stageName is not null)
        {
            var stage = TimeTrackerTools.FindStage(stages, stageName);

            if (stage is null)
            {
                return $"No stage is called '{stageName}'. The stages are: {string.Join(", ", stages.Select(item => item.Name))}.";
            }

            stageId = stage.ItemId;
        }

        DateTime? fromUtc = from is { } first ? TimeTrackerTimeZone.ToUtc(first.ToDateTime(TimeOnly.MinValue), zone) : null;
        DateTime? toUtc = to is { } last ? TimeTrackerTimeZone.ToUtc(last.AddDays(1).ToDateTime(TimeOnly.MinValue), zone) : null;
        var (page, pageSize) = TimeTrackerTools.ReadPaging(arguments);
        var query = TimeEntryListQuery.Build(services.GetRequiredService<ISession>(), TimeTrackerTools.Read(arguments, "userName"), stageId, fromUtc, toUtc, TimeTrackerTools.Read(arguments, "userId"));
        var total = await query.CountAsync(cancellationToken);
        var entries = await query.OrderByDescending(index => index.StartUtc).Skip((page - 1) * pageSize).Take(pageSize).ListAsync(cancellationToken);
        var stagesById = stages.ToDictionary(stage => stage.ItemId, StringComparer.Ordinal);
        var now = services.GetRequiredService<IClock>().UtcNow;
        var items = new List<object>();

        foreach (var entry in entries)
        {
            items.Add(await TimeTrackerTools.EntryAsync(services, entry, stagesById, zone, now, cancellationToken));
        }

        return TimeTrackerTools.Json(TimeTrackerTools.Paged(items, total, page, pageSize, zone));
    }
}
