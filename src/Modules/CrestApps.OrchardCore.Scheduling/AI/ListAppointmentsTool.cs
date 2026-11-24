using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Scheduling.AI;

/// <summary>
/// Lists appointments as the Appointments page does: by status, a search on the customer, and a first day.
/// </summary>
internal sealed class ListAppointmentsTool : SchedulingAdminToolBase
{
    public const string ToolName = "listAppointments";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "status": { "type": "string", "enum": ["Held", "Requested", "Confirmed", "Completed", "NoShow", "Cancelled", "Expired"], "description": "Only appointments in this state. Leave it out for every state." },
        "search": { "type": "string", "description": "Text the customer's name, email or phone number must contain." },
        "fromDate": { "type": "string", "description": "Only appointments that start on or after this day, as yyyy-MM-dd in the site's time zone." },
        "page": { "type": "integer", "minimum": 1, "description": "The page to return, from 1." },
        "pageSize": { "type": "integer", "minimum": 1, "maximum": 50, "description": "How many appointments per page, at most 50. Defaults to 20." }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Lists appointments in start time order, filtered by status, a search on the customer's name, email or phone, and a first day, with paging. Use it to find an appointment's ID.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => SchedulingPermissions.ViewAppointments;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var statusText = SchedulingToolSupport.Read(arguments, "status");
        AppointmentStatus? status = null;

        if (statusText is not null)
        {
            if (!Enum.TryParse<AppointmentStatus>(statusText, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            {
                return "The status must be Held, Requested, Confirmed, Completed, NoShow, Cancelled or Expired.";
            }

            status = parsed;
        }

        var siteZone = await SchedulingToolSupport.GetSiteTimeZoneIdAsync(services);
        var fromText = SchedulingToolSupport.Read(arguments, "fromDate");
        DateTime? fromUtc = null;

        if (fromText is not null)
        {
            if (!DateTime.TryParseExact(fromText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            {
                return "The fromDate must be a day written as yyyy-MM-dd.";
            }

            fromUtc = CalendarTime.ToUtc(day, siteZone);
        }

        var (page, pageSize) = SchedulingToolSupport.ReadPaging(arguments);
        var (total, appointments) = await services.GetRequiredService<AppointmentStore>()
            .PageAsync(status, SchedulingToolSupport.Read(arguments, "search"), fromUtc, (page - 1) * pageSize, pageSize);
        var types = (await services.GetRequiredService<AppointmentTypeStore>().ListAsync()).ToDictionary(type => type.ItemId, StringComparer.Ordinal);
        var items = appointments.Select(appointment => SchedulingToolResults.AppointmentSummary(appointment, types, siteZone)).ToList();

        return SchedulingToolSupport.Json(SchedulingToolSupport.Paged(items, total, page, pageSize));
    }
}
