using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core;

/// <summary>
/// Resolves how many attempts the dialer may make for a campaign record.
/// </summary>
/// <remarks>
/// The subject flow decides first: a "Try again" action that names a maximum records it on the attempt it creates, and
/// that is the limit for the record whatever its dialer profile allows. The profile's Max attempts is the fallback, for a
/// record the flow set no maximum for: a first attempt, one a "Try again" with no maximum created, and one the dialer
/// retried itself because the flow has no rule for how the call ended.
/// </remarks>
public static class DialerAttemptLimit
{
    /// <summary>
    /// Gets the most attempts the dialer may make for the activity.
    /// </summary>
    /// <param name="activity">The campaign record, or <see langword="null"/> when it is not known.</param>
    /// <param name="profile">The dialer profile that dials it.</param>
    /// <returns>The subject flow's maximum when it set one; otherwise the dialer profile's.</returns>
    public static int Resolve(OmnichannelActivity activity, DialerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return activity?.MaxAttempts is > 0 and var maxAttempts
            ? maxAttempts
            : profile.MaxAttempts;
    }
}
