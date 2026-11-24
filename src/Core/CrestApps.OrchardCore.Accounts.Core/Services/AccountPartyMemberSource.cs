using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Accounts.Services;

/// <summary>
/// The members of an account (party kind <see cref="PartyKinds.Account"/>) are the users who sign in for it, kept by the
/// Account Members feature; a user acts for the one account they are a member of. Only the account's admins
/// (<see cref="AccountMemberRole.Admin"/>) have <see cref="PartyAccess.Billing"/>: a plain member (a buyer) acts for the
/// account in what they do themselves, but does not see or settle what the account owes.
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

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetMemberUserIdsAsync(PartyRef party, PartyAccess access, CancellationToken cancellationToken = default)
    {
        if (access == PartyAccess.Member)
        {
            return await GetMemberUserIdsAsync(party, cancellationToken);
        }

        if (party.IsEmpty)
        {
            return [];
        }

        var members = await _accountMemberService.GetMembersAsync(party.Id, cancellationToken) ?? [];

        return [.. members
            .Where(member => member.Role == AccountMemberRole.Admin)
            .Select(member => member.UserId)
            .Distinct(StringComparer.Ordinal)];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PartyRef>> GetPartiesAsync(string userId, PartyAccess access, CancellationToken cancellationToken = default)
    {
        if (access == PartyAccess.Member)
        {
            return await GetPartiesAsync(userId, cancellationToken);
        }

        if (string.IsNullOrEmpty(userId) ||
            await _accountMemberService.FindAccountAsync(userId, cancellationToken) is not { ContentItemId.Length: > 0 } account ||
            !account.TryGet<AccountMembersPart>(out var part) ||
            !(part.Members ?? []).Any(member =>
                member is not null &&
                member.Role == AccountMemberRole.Admin &&
                string.Equals(member.UserId, userId, StringComparison.Ordinal)))
        {
            return [];
        }

        return [new PartyRef(PartyKinds.Account, account.ContentItemId)];
    }
}
