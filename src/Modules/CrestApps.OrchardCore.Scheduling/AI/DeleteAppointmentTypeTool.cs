using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Scheduling.AI;

/// <summary>
/// Deletes an appointment type, as the Delete button of the Appointment Types page does.
/// </summary>
internal sealed class DeleteAppointmentTypeTool : SchedulingAdminToolBase
{
    public const string ToolName = "deleteAppointmentType";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "appointmentTypeId": { "type": "string", "description": "The ID of the type to delete, from listAppointmentTypes." }
      },
      "required": ["appointmentTypeId"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Deletes an appointment type. Appointments already booked keep pointing at it, so turn the type off instead (saveAppointmentType with enabled false) when it has appointments.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => SchedulingPermissions.ManageScheduling;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var store = services.GetRequiredService<AppointmentTypeStore>();
        var id = SchedulingToolSupport.Read(arguments, "appointmentTypeId");
        var type = id is null ? null : await store.FindByIdAsync(id, cancellationToken);

        if (type is null)
        {
            return SchedulingToolResults.AppointmentTypeNotFound;
        }

        await store.DeleteAsync(type, cancellationToken);

        return SchedulingToolSupport.Json(new
        {
            deleted = true,
            id = type.ItemId,
            name = type.Name,
            note = "Appointments and waitlist entries already made for this type are kept.",
        });
    }
}
