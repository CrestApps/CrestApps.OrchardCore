using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;

namespace CrestApps.OrchardCore.Accounts.Services;

/// <summary>
/// The members of an account (party kind <see cref="PartyKinds.Account"/>) are the users who sign in for it, kept by the
/// Account Members feature; a user acts for the one account they are a member of.
/// </summary>
public sealed class AccountPartyMemberSource : IPartyMemberSource
{
    private readonly IAccountMemberService _accountMemberService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountPartyMemberSource"/> class.
    /// </summary>
    /// <param name="accountMemberService">The service that finds the members of an account.</param>
    public AccountPartyMemberSource(IAccountMemberService accountMemberService)
    {
        _accountMemberService = accountMemberService;
    }

    /// <inheritdoc/>
    public string Kind => PartyKinds.Account;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetMemberUserIdsAsync(PartyRef party, CancellationToken cancellationToken = default)
    {
        if (party.IsEmpty)
        {
            return [];
        }

        return await _accountMemberService.GetMemberUserIdsAsync(party.Id, cancellationToken) ?? [];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PartyRef>> GetPartiesAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) ||
            await _accountMemberService.FindAccountAsync(userId, cancellationToken) is not { ContentItemId.Length: > 0 } account)
        {
            return [];
        }

        return [new PartyRef(PartyKinds.Account, account.ContentItemId)];
    }
}
