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
/// Posts an open shift on its own for a team the user manages, as the Open Shifts page's Post form does.
/// </summary>
internal sealed class PostOpenShiftTool : WorkforceAdminToolBase
{
    public const string ToolName = "postOpenShift";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "teamName": { "type": "string", "description": "The team's name or ID. It must be a team the user manages." },
        "date": { "type": "string", "description": "The day of the shift, as yyyy-MM-dd." },
        "start": { "type": "string", "description": "When the shift starts, as HH:mm in the team's time zone. Optional with a template, which gives it." },
        "end": { "type": "string", "description": "When the shift ends, as HH:mm in the team's time zone; an end before the start ends the next day. Optional with a template." },
        "template": { "type": "string", "description": "Optional. A shift template's name or ID: its times, breaks and required skills are used." },
        "breaks": { "type": "array", "items": { "type": "string" }, "description": "Optional. Breaks and activities inside the shift, one per item, as 'HH:mm-HH:mm Activity', such as '13:00-13:30 Meal'; the activity is an activity code's name or short code. Replaces the template's." },
        "notes": { "type": "string", "description": "Optional. Notes the workers see." }
      },
      "required": ["teamName", "date"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Posts a shift nobody has for a team the user manages, on a day and time, optionally from a shift template. The team's settings decide how it is claimed (first come, approval or seniority) and the claim deadline. Eligible workers are notified.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageOpenShifts;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (team, error) = await OpenShiftToolSupport.FindManagedTeamAsync(services, user, WorkforceAdminTools.Read(arguments, "teamName"), cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var date = WorkforceAdminTools.ReadDate(arguments, "date");

        if (date is null)
        {
            return "Give the day of the shift as yyyy-MM-dd.";
        }

        ShiftTemplate template = null;
        var templateName = WorkforceAdminTools.Read(arguments, "template");

        if (templateName is not null)
        {
            template = (await services.GetRequiredService<IShiftTemplateManager>().GetAllAsync(cancellationToken))
                .FirstOrDefault(item => item.ItemId == templateName || WorkforceNames.Normalize(item.Name) == WorkforceNames.Normalize(templateName));

            if (template is null)
            {
                return $"No shift template is called '{templateName}'.";
            }
        }

        var startText = WorkforceAdminTools.Read(arguments, "start");
        var endText = WorkforceAdminTools.Read(arguments, "end");
        TimeOnly start;
        TimeOnly end;

        if (startText is null && endText is null && template is not null)
        {
            start = template.Start;
            end = template.Start.AddMinutes(template.LengthMinutes);
        }
        else if (!PlanningInput.TryParseTime(startText, out start) || !PlanningInput.TryParseTime(endText, out end))
        {
            return "Give when the shift starts and ends as HH:mm, such as 09:00 and 17:00, or a template.";
        }

        var codes = await services.GetRequiredService<IActivityCodeManager>().GetAllAsync(cancellationToken);
        var breaks = WorkforceAdminTools.ReadList(arguments, "breaks");
        IEnumerable<SegmentLine> segments;

        if (breaks.Count > 0)
        {
            var errors = new List<string>();

            segments = SegmentFormat.Read(string.Join('\n', breaks), codes, errors);

            if (errors.Count > 0)
            {
                return $"These breaks or activities aren't understood: {string.Join("; ", errors)}. Write each as 'HH:mm-HH:mm Activity', with an activity code's name or short code.";
            }
        }
        else
        {
            segments = template is null ? [] : SegmentFormat.FromTemplate(template);
        }

        var (from, to) = PlanningInput.OnDay(date.Value, start, end);
        var result = await services.GetRequiredService<IOpenShiftService>().PostAsync(new OpenShift
        {
            TeamId = team.ItemId,
            Date = date.Value,
            Start = from,
            End = to,
            TemplateId = template?.ItemId,
            Segments = SegmentFormat.ToDraft(segments, from),
            RequiredSkillIds = template?.RequiredSkillIds.ToList() ?? [],
            Notes = WorkforceAdminTools.Read(arguments, "notes"),
        }, user.Identity?.Name, cancellationToken);

        if (!result.Succeeded)
        {
            return $"The open shift wasn't posted: {string.Join(" ", result.Errors)}";
        }

        return WorkforceAdminTools.Json(new
        {
            posted = true,
            openShift = await OpenShiftToolSupport.DescribeShiftAsync(services, result.Value, withClaims: false, cancellationToken),
            link = WorkforceAdminTools.GetAdminPath(services, "OpenShifts", "Details", new { id = result.Value.ItemId }),
        });
    }
}
