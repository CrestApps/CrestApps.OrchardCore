using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Removes a line added by hand from a timesheet, as the timesheet page's Remove line button does.
/// </summary>
internal sealed class RemoveTimesheetLineTool : WorkforceAdminToolBase
{
    public const string ToolName = "removeTimesheetLine";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "timesheetId": { "type": "string", "description": "The timesheet's ID, from listTimesheets or getTimesheet." },
        "lineId": { "type": "string", "description": "The line's ID, from getTimesheet. Only lines added by hand (source Manual) can be removed." }
      },
      "required": ["timesheetId", "lineId"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Removes a manual line from a timesheet. It can be added again with addTimesheetLine.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ApproveTimesheets;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (timesheet, error) = await TimeClockAdminTools.FindTimesheetAsync(services, user, WorkforceAdminTools.Read(arguments, "timesheetId"), cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var lineId = WorkforceAdminTools.Read(arguments, "lineId");

        if (lineId is null)
        {
            return "Give the line's ID, from getTimesheet.";
        }

        var line = timesheet.Lines.FirstOrDefault(item => item.Id == lineId);
        var removed = line is null ? null : new { date = WorkforceAdminTools.Day(line.Date), payCodeId = line.PayCodeId, hours = line.Hours, note = line.Note };
        var result = await services.GetRequiredService<ITimesheetService>().RemoveLineAsync(timesheet.ItemId, lineId, user.Identity?.Name, cancellationToken);

        if (!result.Succeeded)
        {
            return "The line wasn't removed: " + string.Join(" ", result.Errors);
        }

        return WorkforceAdminTools.Json(new
        {
            timesheetId = timesheet.ItemId,
            removedLineId = lineId,
            removed,
            totalHours = result.Value?.TotalHours,
        });
    }
}
