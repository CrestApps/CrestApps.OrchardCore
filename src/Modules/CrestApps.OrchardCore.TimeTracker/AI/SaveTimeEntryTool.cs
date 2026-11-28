using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.TimeTracker.AI;

/// <summary>
/// Adds a time entry for someone, or changes one's stage, start, end or note, with a reason that is kept in the entry's
/// history, as the Time Entries edit page does.
/// </summary>
internal sealed class SaveTimeEntryTool : TimeTrackerToolBase
{
    public const string ToolName = "saveTimeEntry";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "entryId": { "type": "string", "description": "The entry to change, from listTimeEntries. Leave it out to add an entry." },
        "userId": { "type": "string", "description": "For a new entry: the user ID of the person whose time it is. Or give userName." },
        "userName": { "type": "string", "description": "For a new entry: the person's user name (login), when the user ID is not known." },
        "stage": { "type": "string", "description": "The stage's name or ID. Required for a new entry; kept when left out on a change." },
        "start": { "type": "string", "description": "When it starts (yyyy-MM-ddTHH:mm) in the Time Tracker's time zone. Required for a new entry; kept when left out on a change." },
        "end": { "type": "string", "description": "When it ends (yyyy-MM-ddTHH:mm). Required for a new entry; kept when left out on a change. An entry still open can be changed without an end." },
        "note": { "type": "string", "description": "Optional. A note on the entry; kept when left out on a change." },
        "reason": { "type": "string", "description": "Why the time is added or changed. Kept in the entry's history." }
      },
      "required": ["reason"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Adds a time entry for a user, or changes an entry's stage, start, end or note. A reason is required and is kept in the entry's history. Time kept by another clock (such as the Workforce time clock) is corrected there instead.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => TimeTrackerPermissions.ManageTimeEntries;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var reason = TimeTrackerTools.Read(arguments, "reason");

        if (reason is null)
        {
            return "Nothing was saved: say why the time is added or changed (reason).";
        }

        var zone = await services.GetRequiredService<TimeTrackerTimeZone>().GetAsync();

        foreach (var name in new[] { "start", "end" })
        {
            if (TimeTrackerTools.Has(arguments, name) && TimeTrackerTools.ReadLocalAsUtc(arguments, name, zone) is null)
            {
                return $"Nothing was saved: give {name} as yyyy-MM-ddTHH:mm.";
            }
        }

        var tracker = services.GetRequiredService<ITimeTrackerService>();
        var stages = await TimeTrackerTools.StagesAsync(services, cancellationToken);
        var stageName = TimeTrackerTools.Read(arguments, "stage");
        var stage = stageName is null ? null : TimeTrackerTools.FindStage(stages, stageName);

        if (stageName is not null && stage is null)
        {
            return $"Nothing was saved: no stage is called '{stageName}'. The stages are: {string.Join(", ", stages.Where(item => item.Enabled).Select(item => item.Name))}.";
        }

        var entryId = TimeTrackerTools.Read(arguments, "entryId");
        var start = TimeTrackerTools.ReadLocalAsUtc(arguments, "start", zone);
        var end = TimeTrackerTools.ReadLocalAsUtc(arguments, "end", zone);
        var note = TimeTrackerTools.Read(arguments, "note");
        TimeTrackerResult result;
        bool created;

        if (entryId is null)
        {
            if (start is null)
            {
                return "Nothing was saved: give when the time starts (start).";
            }

            var person = await FindUserAsync(services, arguments);

            if (person is null)
            {
                return "Nothing was saved: choose whose time it is (an existing userId or userName).";
            }

            result = await tracker.AddAsync(new TimeEntry
            {
                UserId = await services.GetRequiredService<UserManager<IUser>>().GetUserIdAsync(person),
                UserName = await TimeTrackerTools.DisplayNameAsync(services, person, cancellationToken),
                StageId = stage?.ItemId,
                StartUtc = start.Value,
                EndUtc = end,
                Note = note,
                Source = TimeTrackerConstants.Sources.Manager,
            }, user.Identity?.Name, reason, cancellationToken);
            created = true;
        }
        else
        {
            var entry = await tracker.FindAsync(entryId, cancellationToken);

            if (entry is null)
            {
                return $"Nothing was saved: no time entry has the ID '{entryId}'. Use listTimeEntries to find it.";
            }

            result = await tracker.UpdateAsync(
                entryId,
                stage?.ItemId ?? entry.StageId,
                start ?? entry.StartUtc,
                TimeTrackerTools.Has(arguments, "end") ? end : entry.EndUtc,
                TimeTrackerTools.Has(arguments, "note") ? note : entry.Note,
                user.Identity?.Name,
                reason,
                cancellationToken);
            created = false;
        }

        if (!result.Succeeded)
        {
            return "Nothing was saved: " + string.Join(" ", result.Errors);
        }

        await TimeTrackerTools.SaveAsync(services, cancellationToken);

        var now = services.GetRequiredService<IClock>().UtcNow;

        return TimeTrackerTools.Json(new
        {
            saved = true,
            created,
            entry = await TimeTrackerTools.EntryAsync(services, result.Entry, stages.ToDictionary(item => item.ItemId, StringComparer.Ordinal), zone, now, cancellationToken),
            timeZone = zone,
        });
    }

    private static async Task<IUser> FindUserAsync(IServiceProvider services, AIFunctionArguments arguments)
    {
        var userManager = services.GetService<UserManager<IUser>>();

        if (userManager is null)
        {
            return null;
        }

        var userId = TimeTrackerTools.Read(arguments, "userId");

        if (userId is not null)
        {
            return await userManager.FindByIdAsync(userId);
        }

        var userName = TimeTrackerTools.Read(arguments, "userName");

        return userName is null ? null : await userManager.FindByNameAsync(userName);
    }
}
