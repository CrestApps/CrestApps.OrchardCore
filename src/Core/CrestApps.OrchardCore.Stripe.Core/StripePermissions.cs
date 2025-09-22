using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Stripe.Core;

public static class StripePermissions
{
    public static readonly Permission ManageStripeSettings = new("ManageStripeSettings", "Manage Stripe Settings");
}
