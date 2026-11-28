using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Adds or changes a shift in a draft schedule, as the shift editor of the planner board does: the worker or nobody, the
/// day, the start and end in the team's wall-clock time, the template, the breaks and the notes.
/// </summary>
internal sealed class SaveDraftShiftTool : WorkforceAdminToolBase
{
    public const string ToolName = "saveDraftShift";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "scheduleId": { "type": "string", "description": "The schedule's ID, from listShiftSchedules." },
        "shiftId": { "type": "string", "description": "Optional. The draft shift to change, from getShiftSchedule. Leave out to add a shift; only the values given change an existing one." },
        "workerId": { "type": "string", "description": "Optional. The worker, by ID. They must be on the team that day." },
        "workerName": { "type": "string", "description": "Optional. The worker, by name." },
        "open": { "type": "boolean", "description": "Optional. True for a shift nobody has yet (an open shift); it is offered to the team when the schedule is published." },
        "date": { "type": "string", "description": "The day (yyyy-MM-dd), inside the schedule's dates. Needed for a new shift." },
        "start": { "type": "string", "description": "The start in the team's time (HH:mm). Needed for a new shift unless a template's times are used." },
        "end": { "type": "string", "description": "The end in the team's time (HH:mm); at or before the start means the next day." },
        "template": { "type": "string", "description": "Optional. A shift template, by name or ID." },
        "useTemplateTimes": { "type": "boolean", "description": "Optional. Take the start, end and breaks from the template, for those not given here." },
        "breaks": { "type": "array", "items": { "type": "string" }, "description": "Optional. The breaks and activities inside the shift, one per item, such as '13:00-13:30 Meal'; the activity is an activity code's name or short code. Replaces the shift's breaks; an empty list removes them." },
        "notes": { "type": "string", "description": "Optional. Notes for the worker." }
      },
      "required": ["scheduleId"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Adds or changes a shift in a draft schedule: the worker (or nobody, for an open shift), day, start and end, template, breaks and notes. Returns the labor rule problems of that worker's day. Workers see it only once the schedule is published.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageSchedules;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (period, team, error) = await ShiftScheduleToolSupport.FindPeriodAsync(services, user, WorkforceAdminTools.Read(arguments, "scheduleId"), cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var shiftId = WorkforceAdminTools.Read(arguments, "shiftId");
        DraftShift shift;

        if (shiftId is null)
        {
            shift = new DraftShift { ScenarioId = period.PrimaryScenarioId };
        }
        else
        {
            shift = await ShiftScheduleToolSupport.FindDraftAsync(services, period, shiftId, cancellationToken);

            if (shift is null)
            {
                return $"The schedule has no draft shift with the ID '{shiftId}'. Use getShiftSchedule to find it.";
            }
        }

        var isNew = shiftId is null;
        var codes = (await services.GetRequiredService<IActivityCodeManager>().GetAllAsync(cancellationToken)).ToList();

        // The template: the one named, or the shift's own.
        ShiftTemplate template = null;
        var templateText = WorkforceAdminTools.Read(arguments, "template");
        var templates = services.GetRequiredService<IShiftTemplateManager>();

        if (templateText is not null)
        {
            template = (await templates.GetAllAsync(cancellationToken))
                .FirstOrDefault(item => item.ItemId == templateText || WorkforceNames.Normalize(item.Name) == WorkforceNames.Normalize(templateText));

            if (template is null)
            {
                return $"No shift template is called '{templateText}'.";
            }
        }
        else if (!isNew && shift.TemplateId is not null)
        {
            template = await templates.FindByIdAsync(shift.TemplateId, cancellationToken);
        }

        var useTemplateTimes = WorkforceAdminTools.ReadBool(arguments, "useTemplateTimes") == true;

        if (useTemplateTimes && template is null)
        {
            return "Name the template (template) whose times to use.";
        }

        // The worker: the one named, nobody, or the shift's own.
        string workerId = isNew ? null : shift.WorkerId;

        if (WorkforceAdminTools.ReadBool(arguments, "open") == true)
        {
            workerId = null;
        }
        else if (WorkforceAdminTools.Has(arguments, "workerId") || WorkforceAdminTools.Has(arguments, "workerName"))
        {
            var (worker, workerError) = await WorkforceAdminTools.FindWorkerAsync(services, user, arguments, cancellationToken);

            if (workerError is not null)
            {
                return workerError;
            }

            workerId = worker.ItemId;
        }

        var date = WorkforceAdminTools.ReadDate(arguments, "date") ?? (isNew ? (DateOnly?)null : shift.Date);

        if (date is null)
        {
            return WorkforceAdminTools.Has(arguments, "date") ? "Give the day (date) as yyyy-MM-dd." : "Give the shift's day (date) as yyyy-MM-dd.";
        }

        // The times and breaks, as the editor's form carries them: the stored ones unless given, or the template's.
        var startText = WorkforceAdminTools.Read(arguments, "start");
        var endText = WorkforceAdminTools.Read(arguments, "end");
        IList<string> breakLines = WorkforceAdminTools.Has(arguments, "breaks") ? WorkforceAdminTools.ReadList(arguments, "breaks") : null;

        if (useTemplateTimes)
        {
            startText ??= template.Start.ToString("HH:mm", CultureInfo.InvariantCulture);
            endText ??= template.Start.AddMinutes(template.LengthMinutes).ToString("HH:mm", CultureInfo.InvariantCulture);
            breakLines ??= Lines(SegmentFormat.Write(SegmentFormat.FromTemplate(template), codes, template.Start));
        }

        if (!isNew)
        {
            startText ??= shift.Start.ToString("HH:mm", CultureInfo.InvariantCulture);
            endText ??= shift.End.ToString("HH:mm", CultureInfo.InvariantCulture);
            breakLines ??= ShiftScheduleToolSupport.Breaks(shift, codes).ToList();
        }

        if (!PlanningInput.TryParseTime(startText, out var start))
        {
            return "Give when the shift starts (start), such as 09:00.";
        }

        if (!PlanningInput.TryParseTime(endText, out var end))
        {
            return "Give when the shift ends (end), such as 17:00.";
        }

        var errors = new List<string>();
        var segments = SegmentFormat.Read(string.Join('\n', breakLines ?? []), codes, errors);

        if (errors.Count > 0)
        {
            return $"These breaks or activities aren't understood: {string.Join("; ", errors)}. Write each as 'HH:mm-HH:mm Activity', with an activity code's name or short code.";
        }

        var (from, to) = PlanningInput.OnDay(date.Value, start, end);

        shift.WorkerId = workerId;
        shift.Date = date.Value;
        shift.Start = from;
        shift.End = to;
        shift.TemplateId = template?.ItemId;
        shift.Segments = SegmentFormat.ToDraft(segments, from);

        if (WorkforceAdminTools.Has(arguments, "notes"))
        {
            shift.Notes = WorkforceAdminTools.Read(arguments, "notes");
        }

        var planning = services.GetRequiredService<ISchedulePlanningService>();
        var result = await planning.SaveDraftShiftAsync(shift, user.Identity?.Name, cancellationToken);

        if (!result.Succeeded)
        {
            return "The shift was not saved: " + string.Join(" ", result.Errors);
        }

        var saved = result.Value ?? shift;
        var names = await ShiftScheduleToolSupport.WorkerNamesAsync(services, [saved.WorkerId], cancellationToken);
        var templateNames = template is null ? new Dictionary<string, ShiftTemplate>(StringComparer.Ordinal) : new Dictionary<string, ShiftTemplate>(StringComparer.Ordinal) { [template.ItemId] = template };
        IReadOnlyList<LaborRuleViolation> problems = saved.WorkerId is null
            ? []
            : (await planning.CheckPublishAsync(period.ItemId, cancellationToken))
                .Where(violation => violation.WorkerId == saved.WorkerId && (violation.Date is null || violation.Date == saved.Date))
                .ToList();

        return WorkforceAdminTools.Json(new
        {
            saved = true,
            created = isNew,
            scheduleId = period.ItemId,
            team = team.Name,
            shift = ShiftScheduleToolSupport.Describe(saved, names, templateNames, codes),
            laborRuleProblems = ShiftScheduleToolSupport.Problems(problems),
            note = period.PublishedVersion > 0 ? "The schedule is already published: workers see this change when it is published again. To change a published shift now, use changePublishedShift." : null,
        });
    }

    private static string[] Lines(string text)
        => string.IsNullOrEmpty(text) ? [] : text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
