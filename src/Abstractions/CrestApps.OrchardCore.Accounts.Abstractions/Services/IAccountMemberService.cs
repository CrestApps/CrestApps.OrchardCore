using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Accounts.Services;

/// <summary>
/// Finds the account a user signs in for. Registered by the Account Members feature.
/// </summary>
public interface IAccountMemberService
{
    /// <summary>
    /// Returns the published account the user is a member of, or <see langword="null"/>.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<ContentItem> FindAccountAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the content item id of the latest version of any account the user is a member of, other than the one
    /// given, or <see langword="null"/>. A user is a member of one account at most, so this tells an editor that the
    /// user already belongs to another account.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="exceptAccountId">The account being edited, which is not counted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<string> FindOtherAccountIdAsync(string userId, string exceptAccountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the user ids of the members of the published version of an account, each once, or none when the account
    /// isn't found or has no members.
    /// </summary>
    /// <param name="accountId">The account's content item id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<string>> GetMemberUserIdsAsync(string accountId, CancellationToken cancellationToken = default);
}
