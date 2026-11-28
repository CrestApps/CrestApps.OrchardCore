using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using static CrestApps.OrchardCore.Workforce.ContactCenter.AI.ContactCenterWorkforceTools;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.AI;

/// <summary>
/// Lists the adherence exceptions of the teams the user manages, newest first, as the Adherence Exceptions page does, or
/// one worker's exceptions over a range of days.
/// </summary>
internal sealed class ListAdherenceExceptionsTool : ContactCenterWorkforceToolBase
{
    public const string ToolName = "listAdherenceExceptions";

    private const int MaxDays = 92;

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "teamName": { "type": "string", "description": "Optional. One team the user manages (name or ID); its sub-teams are included. Leave out for every team the user manages." },
        "workerId": { "type": "string", "description": "Optional. One worker's ID: lists that worker's exceptions between from and to." },
        "workerName": { "type": "string", "description": "Optional. One worker's name, when the ID isn't known." },
        "from": { "type": "string", "description": "Optional, with a worker only. The first day, yyyy-MM-dd, in the worker's time zone. 30 days ago by default." },
        "to": { "type": "string", "description": "Optional, with a worker only. The last day, yyyy-MM-dd, in the worker's time zone. Today by default; at most 92 days after from." },
        "page": { "type": "integer", "minimum": 1, "description": "Optional. The page, from 1." },
        "pageSize": { "type": "integer", "minimum": 1, "maximum": 50, "description": "Optional. Items per page, 20 by default, at most 50." }
      },
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Lists the adherence exceptions recorded for the teams the user manages (newest first), or for one worker over a range of days, with the time, reason, note and who recorded each.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ViewAdherence;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var adherence = services.GetRequiredService<AdherenceService>();
        var workers = services.GetRequiredService<IStaffMemberManager>();
        var (page, pageSize) = ReadPaging(arguments);
        var teamNames = await TeamNamesAsync(services, cancellationToken);
        IReadOnlyList<AdherenceException> items;
        int total;

        if (Has(arguments, "workerId") || Has(arguments, "workerName"))
        {
            var (worker, error) = await FindWorkerAsync(services, user, arguments, cancellationToken);

            if (worker is null)
            {
                return error;
            }

            if ((Has(arguments, "from") && ReadDate(arguments, "from") is null) || (Has(arguments, "to") && ReadDate(arguments, "to") is null))
            {
                return "The from and to days must be written yyyy-MM-dd.";
            }

            var zone = await adherence.TimeZoneOfAsync(worker, cancellationToken);
            var to = ReadDate(arguments, "to") ?? Today(services, zone);
            var from = ReadDate(arguments, "from") ?? to.AddDays(-30);

            if (to < from)
            {
                return "The to day must not be before the from day.";
            }

            if (to.DayNumber - from.DayNumber > MaxDays)
            {
                return $"Ask for at most {MaxDays} days at a time.";
            }

            var all = (await adherence.ListExceptionsAsync(
                    worker.ItemId,
                    CalendarTime.ToUtc(from.ToDateTime(TimeOnly.MinValue), zone),
                    CalendarTime.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), zone),
                    cancellationToken))
                .OrderByDescending(item => item.StartUtc)
                .ToList();

            total = all.Count;
            items = [.. all.Skip((page - 1) * pageSize).Take(pageSize)];
        }
        else
        {
            var (teamIds, error) = await TeamFilterAsync(services, user, arguments, cancellationToken);

            if (error is not null)
            {
                return error;
            }

            (total, items) = await adherence.PageExceptionsAsync(teamIds, (page - 1) * pageSize, pageSize, cancellationToken);
        }

        var rows = new List<object>(items.Count);
        var cache = new Dictionary<string, (StaffMember Worker, string Zone)>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            if (!cache.TryGetValue(item.WorkerId, out var entry))
            {
                var found = await workers.FindByIdAsync(item.WorkerId, cancellationToken);

                entry = (found, found is null ? "UTC" : await adherence.TimeZoneOfAsync(found, cancellationToken));
                cache[item.WorkerId] = entry;
            }

            rows.Add(new
            {
                id = item.ItemId,
                worker = entry.Worker?.DisplayName ?? item.WorkerId,
                workerId = item.WorkerId,
                team = item.TeamId is not null && teamNames.TryGetValue(item.TeamId, out var teamName) ? teamName : null,
                start = InZone(item.StartUtc, entry.Zone),
                end = InZone(item.EndUtc, entry.Zone),
                timeZone = entry.Zone,
                reason = item.Reason.ToString(),
                note = item.Note,
                recordedBy = item.RecordedBy,
                recordedAt = await LocalAsync(services, item.CreatedUtc),
            });
        }

        return ToJson(Paged(rows, total, page, pageSize));
    }
}
