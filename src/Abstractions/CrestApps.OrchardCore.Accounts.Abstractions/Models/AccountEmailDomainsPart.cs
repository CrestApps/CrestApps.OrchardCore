using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Accounts.Models;

/// <summary>
/// The e-mail domains an account's people write from, such as <c>contoso.com</c>, so a message or request from an
/// address on one of them is filed under the account.
/// </summary>
public sealed class AccountEmailDomainsPart : ContentPart
{
    /// <summary>
    /// Gets or sets the domains, lower case, without the <c>@</c>.
    /// </summary>
    public List<string> Domains { get; set; } = [];
}
