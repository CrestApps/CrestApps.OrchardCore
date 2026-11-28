using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TimeTracker.Core.Services;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.TimeTracker.AI;

/// <summary>
/// Totals tracked time per person (and optionally per day) by category and by stage, with the service the Hours by
/// Category report uses.
/// </summary>
internal sealed class GetTimeTotalsTool : TimeTrackerToolBase
{
    public const string ToolName = "getTimeTotals";

    private const int MaxDays = 366;

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "from": { "type": "string", "description": "The first day (yyyy-MM-dd), in the Time Tracker's time zone." },
        "to": { "type": "string", "description": "The last day (yyyy-MM-dd), included. At most 366 days after from." },
        "userId": { "type": "string", "description": "Optional. The user ID of one person." },
        "userName": { "type": "string", "description": "Optional. Text in the person's name." },
        "byDay": { "type": "boolean", "description": "true for one row per person and day; false (the default) for one row per person." },
        "page": { "type": "integer", "minimum": 1, "description": "The page of rows, from 1." },
        "pageSize": { "type": "integer", "minimum": 1, "maximum": 50, "description": "Rows per page, 20 by default, at most 50." }
      },
      "required": ["from", "to"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Totals tracked time per person, optionally per day, by category (such as Payable or Billable, with each category's factor) and by stage, for a range of days. Use it for \"how many hours did X work\". Time still running counts up to now.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => TimeTrackerPermissions.ViewTimeEntries;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var from = TimeTrackerTools.ReadDate(arguments, "from");
        var to = TimeTrackerTools.ReadDate(arguments, "to");

        if (from is null || to is null)
        {
            return "Give the first and last days (from and to) as yyyy-MM-dd.";
        }

        if (from > to)
        {
            return "The first day (from) must not be after the last day (to).";
        }

        if (to.Value.DayNumber - from.Value.DayNumber >= MaxDays)
        {
            return string.Create(CultureInfo.InvariantCulture, $"Ask for at most {MaxDays} days at a time.");
        }

        var zone = await services.GetRequiredService<TimeTrackerTimeZone>().GetAsync();
        var userId = TimeTrackerTools.Read(arguments, "userId");
        var userName = TimeTrackerTools.Read(arguments, "userName");
        var byDay = TimeTrackerTools.ReadBool(arguments, "byDay") == true;
        var query = new TimeEntryQuery
        {
            UserIds = userId is null ? null : new[] { userId },
            FromUtc = TimeTrackerTimeZone.ToUtc(from.Value.ToDateTime(TimeOnly.MinValue), zone),
            ToUtc = TimeTrackerTimeZone.ToUtc(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), zone),
        };
        var rows = (await services.GetRequiredService<ITimeTrackerService>().GetTotalsAsync(query, zone, byDay, cancellationToken))
            .Where(row => userName is null || (row.UserName?.Contains(userName, StringComparison.CurrentCultureIgnoreCase) ?? false))
            .ToList();
        var stages = (await TimeTrackerTools.StagesAsync(services, cancellationToken)).ToDictionary(stage => stage.ItemId, StringComparer.Ordinal);
        var categories = (await services.GetRequiredService<TimeCategoryStore>().GetAllAsync(cancellationToken)).ToDictionary(category => category.ItemId, StringComparer.Ordinal);
        var (page, pageSize) = TimeTrackerTools.ReadPaging(arguments);
        var items = rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => (object)new
            {
                userId = row.UserId,
                user = row.UserName,
                date = row.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                totalHours = TimeTrackerTools.Hours(row.TotalMinutes),
                hoursByCategory = row.MinutesByCategory
                    .Where(pair => categories.ContainsKey(pair.Key))
                    .OrderBy(pair => categories[pair.Key].Position)
                    .Select(pair => new { category = categories[pair.Key].Name, factor = categories[pair.Key].Factor, hours = TimeTrackerTools.Hours(pair.Value) })
                    .ToList(),
                hoursByStage = row.MinutesByStage
                    .Select(pair => new { stage = stages.GetValueOrDefault(pair.Key)?.Name ?? "(no stage)", hours = TimeTrackerTools.Hours(pair.Value) })
                    .ToList(),
            })
            .ToList();

        return TimeTrackerTools.Json(new
        {
            from = from.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            to = to.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            items,
            total = rows.Count,
            page,
            pageSize,
            truncated = ((page - 1) * pageSize) + items.Count < rows.Count,
            timeZone = zone,
        });
    }
}
