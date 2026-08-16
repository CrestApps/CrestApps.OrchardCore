using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Stores the user account created during a subscription flow and the password used to sign it in or roll it back.
/// </summary>
public class CustomerCreatedDuringSubscriptionFlow
{
    /// <summary>
    /// Gets or sets the Orchard Core user created during the flow.
    /// </summary>
    public User User { get; set; }

    /// <summary>
    /// Gets or sets the password generated for the created user.
    /// </summary>
    public string Password { get; set; }
}
