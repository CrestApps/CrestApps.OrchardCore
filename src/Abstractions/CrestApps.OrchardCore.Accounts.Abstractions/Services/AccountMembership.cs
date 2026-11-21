using System.Security.Claims;
using CrestApps.OrchardCore.Accounts.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Accounts.Services;

/// <summary>
/// The account a signed-in user is a member of, and the member they are in it.
/// </summary>
/// <param name="Account">The published account.</param>
/// <param name="Member">The user's member entry in the account.</param>
public sealed record AccountMembership(ContentItem Account, AccountMember Member)
{
    /// <summary>
    /// Gets whether the member manages the account for the others.
    /// </summary>
    public bool IsAdmin => Member?.Role == AccountMemberRole.Admin;
}

/// <summary>
/// Finds the membership of a user, so an account page and the account menu link to it agree on who may open it.
/// </summary>
public static class AccountMemberServiceExtensions
{
    /// <summary>
    /// Returns the account the user is a member of with their member entry, or <see langword="null"/> when the user
    /// is not signed in or is in no published account.
    /// </summary>
    /// <param name="accountMemberService">The account member service.</param>
    /// <param name="user">The signed-in user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task<AccountMembership> FindMembershipAsync(this IAccountMemberService accountMemberService, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountMemberService);

        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var account = await accountMemberService.FindAccountAsync(userId, cancellationToken);
        var member = account is not null && account.TryGet<AccountMembersPart>(out var part) ? part.FindMember(userId) : null;

        return member is null ? null : new AccountMembership(account, member);
    }
}
