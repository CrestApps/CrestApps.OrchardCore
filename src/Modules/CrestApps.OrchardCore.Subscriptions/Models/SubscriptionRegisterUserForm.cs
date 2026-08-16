using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Subscriptions.Models;

/// <summary>
/// Extends the Orchard Core user registration form with subscription-specific password state.
/// </summary>
public sealed class SubscriptionRegisterUserForm : RegisterUserForm
{
    /// <summary>
    /// Gets or sets a value indicating whether the subscription registration already has a saved password.
    /// </summary>
    public bool HasSavedPassword { get; set; }
}
