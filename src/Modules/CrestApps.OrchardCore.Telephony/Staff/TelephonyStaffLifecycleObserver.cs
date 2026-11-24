using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Telephony.Core.Services;

namespace CrestApps.OrchardCore.Telephony.Staff;

/// <summary>
/// Frees the phone extension of a person who leaves, so it can be given to someone else and calls to it don't ring a
/// person who is gone.
/// </summary>
internal sealed class TelephonyStaffLifecycleObserver : IStaffLifecycleObserver
{
    private readonly ITelephonyExtensionManager _extensions;

    public TelephonyStaffLifecycleObserver(ITelephonyExtensionManager extensions)
    {
        _extensions = extensions;
    }

    public async Task ChangedAsync(StaffChangedContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Terminated || string.IsNullOrEmpty(context.StaffMember.UserId))
        {
            return;
        }

        if (await _extensions.FindByUserIdAsync(context.StaffMember.UserId, cancellationToken) is { } extension)
        {
            extension.UserId = null;
            extension.UserName = null;
            await _extensions.UpdateAsync(extension, cancellationToken: cancellationToken);
        }
    }
}
