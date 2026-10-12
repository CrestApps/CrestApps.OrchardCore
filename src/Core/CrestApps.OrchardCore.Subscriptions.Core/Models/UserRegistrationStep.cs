using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Represents user registration data collected during a subscription flow.
/// </summary>
public sealed class UserRegistrationStep
{
    /// <summary>
    /// Gets or sets a value indicating whether the subscriber chose to continue as a guest.
    /// </summary>
    public bool IsGuest { get; set; }

    /// <summary>
    /// Gets or sets the user account to create for the subscriber.
    /// </summary>
    public User User { get; set; }
}
