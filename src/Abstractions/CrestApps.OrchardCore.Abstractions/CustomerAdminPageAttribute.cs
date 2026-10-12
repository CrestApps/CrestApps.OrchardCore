namespace CrestApps.OrchardCore.Commerce;

/// <summary>
/// Marks an admin page that customers use for their own data, such as their plans, their sites or their
/// transactions. A signed-in user can open such a page without the Access admin panel permission, which customers
/// don't have. The page still enforces its own permissions, so this opens nothing else in the admin: the dashboard
/// and every other admin page stay closed to them.
/// </summary>
/// <remarks>
/// Apply it to a controller whose actions check the customer's own permission (for example "View own transactions")
/// and only ever return that customer's data. The Commerce feature grants the access.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class CustomerAdminPageAttribute : Attribute
{
}
