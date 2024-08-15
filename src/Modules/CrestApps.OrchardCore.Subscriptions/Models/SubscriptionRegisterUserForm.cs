using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Subscriptions.Models;

public sealed class SubscriptionRegisterUserForm : RegisterUserForm
{
    public bool HasSavedPassword { get; set; }
}
