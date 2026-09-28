using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The dialing settings a queued callback is placed with. A callback is not campaign work, so it has no stored dialer
/// profile of its own; it is dialed like a preview dial -- the agent accepts, then the platform calls -- from the
/// provider's default caller id.
/// </summary>
/// <remarks>
/// The caller asked to be called back, so neither the do-not-call list nor the calling window may refuse the call: a
/// customer who pressed the key and hung up expecting a call is exactly who must be phoned.
/// </remarks>
public static class QueueCallbackDialerProfile
{
    /// <summary>
    /// The identifier the built-in profile carries on the dial it places. It is never stored.
    /// </summary>
    public const string Id = "__queue-callback__";

    /// <summary>
    /// Whether a dialer profile identifier names the built-in callback profile.
    /// </summary>
    /// <param name="profileId">The dialer profile identifier.</param>
    public static bool IsCallbackProfile(string profileId)
        => string.Equals(profileId, Id, StringComparison.Ordinal);

    /// <summary>
    /// Creates the built-in callback profile.
    /// </summary>
    public static DialerProfile Create()
        => new()
        {
            ItemId = Id,
            Name = "Queued callback",
            Mode = DialerMode.Preview,
            Enabled = true,
            CallsPerAgent = 1,
            MaxAttempts = 3,
            RespectDoNotCall = false,
            EnforceCallingWindow = false,
        };
}
