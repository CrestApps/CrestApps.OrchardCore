using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TimeTracker.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.TimeTracker.AI;

/// <summary>
/// Deletes a time entry with a reason, as the Time Entries page's Delete does. The entry is gone; the reason and what it
/// was go to the observers (the Audit Trail when it is on).
/// </summary>
internal sealed class DeleteTimeEntryTool : TimeTrackerToolBase
{
    public const string ToolName = "deleteTimeEntry";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "entryId": { "type": "string", "description": "The entry to delete, from listTimeEntries." },
        "reason": { "type": "string", "description": "Why the time is removed." }
      },
      "required": ["entryId", "reason"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Deletes a time entry, with a reason. It cannot be undone. Time kept by another clock (such as the Workforce time clock) is corrected there instead.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => TimeTrackerPermissions.ManageTimeEntries;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var entryId = TimeTrackerTools.Read(arguments, "entryId");

        if (entryId is null)
        {
            return "Give the ID of the entry to delete, from listTimeEntries.";
        }

        var result = await services.GetRequiredService<ITimeTrackerService>().DeleteAsync(entryId, user.Identity?.Name, TimeTrackerTools.Read(arguments, "reason"), cancellationToken);

        if (!result.Succeeded)
        {
            return "Nothing was deleted: " + string.Join(" ", result.Errors);
        }

        await TimeTrackerTools.SaveAsync(services, cancellationToken);

        var zone = await services.GetRequiredService<TimeTrackerTimeZone>().GetAsync();

        return TimeTrackerTools.Json(new
        {
            deleted = true,
            id = result.Entry.ItemId,
            user = result.Entry.UserName,
            start = TimeTrackerTools.Local(result.Entry.StartUtc, zone),
            end = TimeTrackerTools.Local(result.Entry.EndUtc, zone),
            timeZone = zone,
        });
    }
}
