using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Scheduling.AI;

/// <summary>
/// Adds a customer to the waitlist of an appointment type, as the Add form of the Waitlist page does.
/// </summary>
internal sealed class AddToAppointmentWaitlistTool : SchedulingAdminToolBase
{
    public const string ToolName = "addToAppointmentWaitlist";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "appointmentTypeId": { "type": "string", "description": "The appointment type the customer wants, from listAppointmentTypes." },
        "fromDate": { "type": "string", "description": "The first day the customer can come, as yyyy-MM-dd in the site's time zone." },
        "toDate": { "type": "string", "description": "The last day the customer can come, as yyyy-MM-dd, on or after fromDate." },
        "customerName": { "type": "string", "description": "The customer's name." },
        "customerEmail": { "type": "string", "description": "The customer's email address." },
        "customerPhone": { "type": "string", "description": "The customer's phone number." }
      },
      "required": ["appointmentTypeId", "fromDate", "toDate", "customerName"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Adds a customer to the waitlist of an appointment type for a range of days. They are told when a time opens (through the Waitlist Slot Available workflow event).";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => SchedulingPermissions.ManageAppointments;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var typeId = SchedulingToolSupport.Read(arguments, "appointmentTypeId");
        var type = typeId is null ? null : await services.GetRequiredService<AppointmentTypeStore>().FindByIdAsync(typeId, cancellationToken);

        if (type is null)
        {
            return SchedulingToolResults.AppointmentTypeNotFound;
        }

        var siteZone = await SchedulingToolSupport.GetSiteTimeZoneIdAsync(services);
        var entry = WaitlistEntryRules.NewEntry(
            type.ItemId,
            SchedulingToolSupport.Read(arguments, "fromDate"),
            SchedulingToolSupport.Read(arguments, "toDate"),
            SchedulingToolSupport.Read(arguments, "customerName"),
            SchedulingToolSupport.Read(arguments, "customerEmail"),
            SchedulingToolSupport.Read(arguments, "customerPhone"),
            siteZone,
            services.GetRequiredService<IClock>().UtcNow);

        if (entry is null)
        {
            return "Nothing was added: give the customer's name and the days as yyyy-MM-dd, the last on or after the first.";
        }

        await services.GetRequiredService<WaitlistStore>().CreateAsync(entry, cancellationToken);

        return SchedulingToolSupport.Json(new
        {
            added = true,
            entry = SchedulingToolResults.WaitlistItem(entry, new Dictionary<string, Models.AppointmentType>(StringComparer.Ordinal) { [type.ItemId] = type }, siteZone),
            timeZoneId = siteZone,
        });
    }
}
