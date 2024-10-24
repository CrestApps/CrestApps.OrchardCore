using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public class CustomerCreatedDuringSubscriptionFlow
{
    public User User { get; set; }

    public string Password { get; set; }
}
