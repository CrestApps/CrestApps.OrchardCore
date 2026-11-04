using CrestApps.OrchardCore.Accounts.Indexes;
using OrchardCore.ContentManagement;
using YesSql;

namespace CrestApps.OrchardCore.Accounts.Services;

/// <summary>
/// Finds the account a user signs in for from the <see cref="AccountMemberIndex"/>.
/// </summary>
public sealed class AccountMemberService : IAccountMemberService
{
    private readonly ISession _session;
    private readonly Dictionary<string, ContentItem> _accountsByUser = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountMemberService"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public AccountMemberService(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public async Task<ContentItem> FindAccountAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        if (!_accountsByUser.TryGetValue(userId, out var account))
        {
            account = await _session
                .Query<ContentItem, AccountMemberIndex>(index => index.UserId == userId && index.Published)
                .FirstOrDefaultAsync(cancellationToken);

            _accountsByUser[userId] = account;
        }

        return account;
    }

    /// <inheritdoc/>
    public async Task<string> FindOtherAccountIdAsync(string userId, string exceptAccountId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var rows = await _session
            .QueryIndex<AccountMemberIndex>(index => index.UserId == userId && index.Latest)
            .ListAsync(cancellationToken);

        return rows
            .Select(row => row.AccountContentItemId)
            .FirstOrDefault(accountId => !string.Equals(accountId, exceptAccountId, StringComparison.Ordinal));
    }
}
