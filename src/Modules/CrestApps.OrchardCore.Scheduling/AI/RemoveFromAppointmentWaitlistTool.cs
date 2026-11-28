using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Scheduling.AI;

/// <summary>
/// Takes a customer off the waitlist, as the Remove button of the Waitlist page does: the entry is kept with status Removed.
/// </summary>
internal sealed class RemoveFromAppointmentWaitlistTool : SchedulingAdminToolBase
{
    public const string ToolName = "removeFromAppointmentWaitlist";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "waitlistEntryId": { "type": "string", "description": "The waitlist entry's ID, from listAppointmentWaitlist." }
      },
      "required": ["waitlistEntryId"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Takes a customer off the waitlist. The entry is kept with status Removed.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => SchedulingPermissions.ManageAppointments;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var store = services.GetRequiredService<WaitlistStore>();
        var id = SchedulingToolSupport.Read(arguments, "waitlistEntryId");
        var entry = id is null ? null : await store.FindByIdAsync(id, cancellationToken);

        if (entry is null)
        {
            return SchedulingToolResults.WaitlistEntryNotFound;
        }

        var previous = entry.Status;

        if (previous != WaitlistStatus.Removed)
        {
            entry.Status = WaitlistStatus.Removed;
            await store.UpdateAsync(entry, cancellationToken);
        }

        return SchedulingToolSupport.Json(new
        {
            id = entry.ItemId,
            customerName = entry.CustomerName,
            previousStatus = previous.ToString(),
            status = entry.Status.ToString(),
            changed = previous != WaitlistStatus.Removed,
        });
    }
}
