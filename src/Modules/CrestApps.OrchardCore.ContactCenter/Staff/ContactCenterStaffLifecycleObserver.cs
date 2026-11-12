using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Staff;

/// <summary>
/// Signs a person who leaves out of the Contact Center, so no more calls or messages are routed to them.
/// </summary>
internal sealed class ContactCenterStaffLifecycleObserver : IStaffLifecycleObserver
{
    private readonly IAgentPresenceManager _presenceManager;
    private readonly ILogger _logger;

    public ContactCenterStaffLifecycleObserver(
        IAgentPresenceManager presenceManager,
        ILogger<ContactCenterStaffLifecycleObserver> logger)
    {
        _presenceManager = presenceManager;
        _logger = logger;
    }

    public async Task ChangedAsync(StaffChangedContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Terminated || string.IsNullOrEmpty(context.StaffMember.UserId))
        {
            return;
        }

        await _presenceManager.SignOutAsync(context.StaffMember.UserId, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Staff member {StaffMemberId} left and was signed out of the Contact Center.", context.StaffMember.ItemId.SanitizeLogValue());
        }
    }
}
