using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Scheduling.AI;

/// <summary>
/// Gets one appointment, as its admin page shows it, without the customer's manage link.
/// </summary>
internal sealed class GetAppointmentTool : SchedulingAdminToolBase
{
    public const string ToolName = "getAppointment";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "appointmentId": { "type": "string", "description": "The appointment's ID, from listAppointments." }
      },
      "required": ["appointmentId"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Gets one appointment: type, time, status, resources, customer, notes, cancel reason and the calendar event it sits on.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => SchedulingPermissions.ViewAppointments;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var id = SchedulingToolSupport.Read(arguments, "appointmentId");
        var appointment = id is null ? null : await services.GetRequiredService<IAppointmentService>().FindByIdAsync(id, cancellationToken);

        if (appointment is null)
        {
            return SchedulingToolResults.AppointmentNotFound;
        }

        var type = string.IsNullOrEmpty(appointment.AppointmentTypeId) ? null : await services.GetRequiredService<AppointmentTypeStore>().FindByIdAsync(appointment.AppointmentTypeId, cancellationToken);
        var resources = await services.GetRequiredService<SchedulingResourceStore>().GetAsync(appointment.ResourceIds ?? [], cancellationToken);
        var siteZone = await SchedulingToolSupport.GetSiteTimeZoneIdAsync(services);
        var adminPath = SchedulingToolSupport.GetAdminPath(services, "Appointments", "Detail", new { id = appointment.ItemId });

        return SchedulingToolSupport.Json(SchedulingToolResults.AppointmentDetail(appointment, type, resources, siteZone, adminPath));
    }
}
