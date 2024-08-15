using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public sealed class UserRegistrationStep
{
    public bool IsGuest { get; set; }

    public User User { get; set; }
}
