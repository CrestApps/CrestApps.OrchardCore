namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// The OAuth scopes of UCP account linking.
/// </summary>
public static class UcpIdentityScopes
{
    /// <summary>
    /// Lets an agent check out as the linked buyer, with their saved addresses.
    /// </summary>
    public const string CheckoutManage = "dev.ucp.shopping.checkout:manage";

    /// <summary>
    /// Lets an agent read the linked buyer's orders.
    /// </summary>
    public const string OrderRead = "dev.ucp.shopping.order:read";

    /// <summary>
    /// Lets an agent change the linked buyer's orders.
    /// </summary>
    public const string OrderManage = "dev.ucp.shopping.order:manage";
}
