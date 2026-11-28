using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Scheduling.AI;

/// <summary>
/// Confirms an appointment, or marks it completed or missed, as the Confirm, Complete and No-show buttons of its page do.
/// </summary>
internal sealed class SetAppointmentStatusTool : SchedulingAdminToolBase
{
    public const string ToolName = "setAppointmentStatus";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "appointmentId": { "type": "string", "description": "The appointment's ID, from listAppointments." },
        "status": { "type": "string", "enum": ["Confirmed", "Completed", "NoShow"], "description": "Confirmed confirms a held or requested appointment; Completed or NoShow marks a booked appointment as taken place or missed." }
      },
      "required": ["appointmentId", "status"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Confirms a requested appointment, or marks a past one completed or no-show. Use changeAppointment to move or cancel. Confirming updates the customer's calendar invitation and, like every status change, can start workflows that message the customer, so get a clear yes first.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => SchedulingPermissions.ManageAppointments;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var status = SchedulingToolSupport.Read(arguments, "status");

        if (status is null || !(status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase) || status.Equals("Completed", StringComparison.OrdinalIgnoreCase) || status.Equals("NoShow", StringComparison.OrdinalIgnoreCase)))
        {
            return "The status must be Confirmed, Completed or NoShow. Use changeAppointment to move or cancel an appointment.";
        }

        var service = services.GetRequiredService<IAppointmentService>();
        var id = SchedulingToolSupport.Read(arguments, "appointmentId");
        var appointment = id is null ? null : await service.FindByIdAsync(id, cancellationToken);

        if (appointment is null)
        {
            return SchedulingToolResults.AppointmentNotFound;
        }

        var previous = appointment.Status.ToString();
        var actor = user.Identity?.Name;
        var result = status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase)
            ? await service.ConfirmAsync(appointment, actor, cancellationToken)
            : await service.CompleteAsync(appointment, noShow: status.Equals("NoShow", StringComparison.OrdinalIgnoreCase), actor, cancellationToken);

        if (!result.Succeeded)
        {
            return "The appointment was not changed: " + string.Join(" ", result.Errors);
        }

        return SchedulingToolSupport.Json(new
        {
            id = appointment.ItemId,
            previousStatus = previous,
            status = appointment.Status.ToString(),
            changed = previous != appointment.Status.ToString(),
        });
    }
}
