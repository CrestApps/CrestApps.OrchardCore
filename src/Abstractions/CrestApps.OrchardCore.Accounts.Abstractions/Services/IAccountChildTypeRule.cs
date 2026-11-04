using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Accounts.Services;

/// <summary>
/// Decides which content types can belong to an account, and so are offered in an account type's list. The Accounts
/// module knows no child types of its own: each module that has records an account holds registers a rule.
/// </summary>
public interface IAccountChildTypeRule
{
    /// <summary>
    /// Determines whether items of the content type can belong to an account.
    /// </summary>
    /// <param name="definition">The content type definition.</param>
    bool CanBelongToAccount(ContentTypeDefinition definition);

    /// <summary>
    /// Determines whether items of the content type must never belong to an account, even when another rule allows
    /// it. Such a type is also taken out of every account type's list.
    /// </summary>
    /// <param name="definition">The content type definition.</param>
    bool MustNotBelongToAccount(ContentTypeDefinition definition);
}
