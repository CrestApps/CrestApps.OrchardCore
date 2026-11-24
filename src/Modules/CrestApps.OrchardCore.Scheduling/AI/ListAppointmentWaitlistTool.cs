using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Scheduling.AI;

/// <summary>
/// Lists the waitlist, newest first, as the Waitlist page does.
/// </summary>
internal sealed class ListAppointmentWaitlistTool : SchedulingAdminToolBase
{
    public const string ToolName = "listAppointmentWaitlist";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "page": { "type": "integer", "minimum": 1, "description": "The page to return, from 1." },
        "pageSize": { "type": "integer", "minimum": 1, "maximum": 50, "description": "How many entries per page, at most 50. Defaults to 20." }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Lists the customers waiting for an opening, newest first, with the appointment type, the days they can come and their status (Waiting, Notified, Booked or Removed).";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => SchedulingPermissions.ManageAppointments;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (page, pageSize) = SchedulingToolSupport.ReadPaging(arguments);
        var (total, entries) = await services.GetRequiredService<WaitlistStore>().PageAsync((page - 1) * pageSize, pageSize);
        var types = (await services.GetRequiredService<AppointmentTypeStore>().ListAsync()).ToDictionary(type => type.ItemId, StringComparer.Ordinal);
        var siteZone = await SchedulingToolSupport.GetSiteTimeZoneIdAsync(services);
        var items = entries.Select(entry => SchedulingToolResults.WaitlistItem(entry, types, siteZone)).ToList();

        return SchedulingToolSupport.Json(SchedulingToolSupport.Paged(items, total, page, pageSize, siteZone));
    }
}
