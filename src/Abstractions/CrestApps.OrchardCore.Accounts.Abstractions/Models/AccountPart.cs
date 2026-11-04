using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Accounts.Models;

/// <summary>
/// Marks a content type as an account type: the business or household that contacts, orders, assets and work orders
/// belong to. The part holds no data; other modules add their own parts to the account type.
/// </summary>
public sealed class AccountPart : ContentPart
{
}
