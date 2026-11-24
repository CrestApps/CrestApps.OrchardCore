using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Confirms or excuses attendance exceptions, as the Attendance page's Decide form and bulk actions do.
/// </summary>
internal sealed class DecideAttendanceExceptionTool : WorkforceAdminToolBase
{
    public const string ToolName = "decideAttendanceException";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "exceptionIds": { "type": "array", "items": { "type": "string" }, "minItems": 1, "maxItems": 20, "description": "The exceptions' IDs, from listAttendanceExceptions or getTimesheet. Up to 20." },
        "decision": { "type": "string", "enum": ["Confirm", "Excuse"], "description": "Confirm adds the exceptions' attendance points to the worker; Excuse clears them." },
        "note": { "type": "string", "description": "For Excuse, optional: why." }
      },
      "required": ["exceptionIds", "decision"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Confirms one or more attendance exceptions (which adds their points) or excuses them with a note.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ApproveTimesheets;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var ids = WorkforceAdminTools.ReadList(arguments, "exceptionIds");
        var decision = WorkforceAdminTools.Read(arguments, "decision");

        if (ids.Count == 0)
        {
            return "Give the IDs of the exceptions to decide, from listAttendanceExceptions.";
        }

        if (ids.Count > WorkforceAdminTools.MaxBulk)
        {
            return $"Decide at most {WorkforceAdminTools.MaxBulk} exceptions at a time.";
        }

        var excuse = string.Equals(decision, "Excuse", StringComparison.OrdinalIgnoreCase);

        if (!excuse && !string.Equals(decision, "Confirm", StringComparison.OrdinalIgnoreCase))
        {
            return "The decision must be Confirm or Excuse.";
        }

        var note = WorkforceAdminTools.Read(arguments, "note");
        var attendance = services.GetRequiredService<IAttendanceService>();
        var scope = await services.GetRequiredService<IWorkforceScopeService>().GetManagedScopeAsync(user, cancellationToken);
        var actor = user.Identity?.Name;
        var results = new List<object>();
        var decided = 0;

        foreach (var id in ids)
        {
            var exception = await attendance.FindByIdAsync(id, cancellationToken);

            if (exception is null || !scope.Covers(exception.TeamId))
            {
                results.Add(new { id, changed = false, error = "No attendance exception with this ID is in the teams the user manages." });

                continue;
            }

            var result = excuse
                ? await attendance.ExcuseAsync(id, note, actor, cancellationToken)
                : await attendance.ConfirmAsync(id, actor, cancellationToken);

            if (result.Succeeded)
            {
                decided++;
                results.Add(new { id, changed = true, status = result.Value?.Status.ToString(), points = result.Value?.Points });
            }
            else
            {
                results.Add(new { id, changed = false, error = string.Join(" ", result.Errors) });
            }
        }

        return WorkforceAdminTools.Json(new
        {
            decision = excuse ? "Excuse" : "Confirm",
            decided,
            results,
        });
    }
}
