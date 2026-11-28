using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Adds a line by hand to a timesheet, as the timesheet page's Add line form does.
/// </summary>
internal sealed class AddTimesheetLineTool : WorkforceAdminToolBase
{
    public const string ToolName = "addTimesheetLine";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "timesheetId": { "type": "string", "description": "The timesheet's ID, from listTimesheets or getTimesheet." },
        "date": { "type": "string", "description": "The day of the line (yyyy-MM-dd), inside the timesheet's pay period." },
        "payCodeId": { "type": "string", "description": "The pay code's ID, from getTimesheet's payCodes, such as a bonus or an allowance." },
        "hours": { "type": "number", "exclusiveMinimum": 0, "maximum": 24, "description": "The hours, more than 0 and up to 24." },
        "note": { "type": "string", "description": "Optional. Why the line is added." }
      },
      "required": ["timesheetId", "date", "payCodeId", "hours"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Adds a manual line to a timesheet: a pay code (such as a bonus or an allowance), hours on a date and a note. It can be taken off again with removeTimesheetLine.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ApproveTimesheets;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (timesheet, error) = await TimeClockAdminTools.FindTimesheetAsync(services, user, WorkforceAdminTools.Read(arguments, "timesheetId"), cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var date = WorkforceAdminTools.ReadDate(arguments, "date");
        var hours = WorkforceAdminTools.ReadDecimal(arguments, "hours");

        if (date is null)
        {
            return "Give the line's day as yyyy-MM-dd.";
        }

        if (hours is null)
        {
            return "Give the line's hours, more than 0 and up to 24.";
        }

        var existing = timesheet.Lines.Select(line => line.Id).Where(id => id is not null).ToHashSet(StringComparer.Ordinal);

        // The service checks the day, the hours, the pay code and whether the timesheet can still change.
        var result = await services.GetRequiredService<ITimesheetService>().AddLineAsync(timesheet.ItemId, new TimesheetLine
        {
            Date = date.Value,
            PayCodeId = WorkforceAdminTools.Read(arguments, "payCodeId"),
            Hours = hours.Value,
            Note = WorkforceAdminTools.Read(arguments, "note"),
        }, user.Identity?.Name, cancellationToken);

        if (!result.Succeeded)
        {
            return "The line wasn't added: " + string.Join(" ", result.Errors);
        }

        var added = result.Value?.Lines
            .FirstOrDefault(line => line.Source == TimesheetLineSource.Manual && line.Id is not null && !existing.Contains(line.Id));

        return WorkforceAdminTools.Json(new
        {
            timesheetId = timesheet.ItemId,
            lineId = added?.Id,
            date = WorkforceAdminTools.Day(date),
            payCodeId = added?.PayCodeId,
            hours = added?.Hours ?? hours.Value,
            totalHours = result.Value?.TotalHours,
        });
    }
}
