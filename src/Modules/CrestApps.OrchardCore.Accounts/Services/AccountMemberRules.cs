using CrestApps.OrchardCore.Accounts.Models;
using Microsoft.AspNetCore.Identity;
using OrchardCore.ContentManagement;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Accounts.Services;

/// <summary>
/// Turns the members typed on an account (a user name or email and a role each) into account members, with the rules of
/// the members editor: each user must exist (found by email, then by user name), a user listed twice counts once, a user
/// may belong to one account only, and a member keeps the contact it was linked to. The members editor and the AI tools
/// both call it; each turns the problems into its own sentences.
/// </summary>
internal static class AccountMemberRules
{
    /// <summary>
    /// Resolves the members typed on an account.
    /// </summary>
    /// <param name="userManager">The user manager.</param>
    /// <param name="accountMemberService">The account member service.</param>
    /// <param name="contentManager">The content manager, to name the other account of a user who already has one.</param>
    /// <param name="part">The account's members part, as stored.</param>
    /// <param name="rows">The users (user name or email) and their roles, in order; blank users are skipped.</param>
    /// <returns>The members, and what is wrong with the rows that could not be resolved.</returns>
    public static async Task<AccountMemberResolution> ResolveAsync(
        UserManager<IUser> userManager,
        IAccountMemberService accountMemberService,
        IContentManager contentManager,
        AccountMembersPart part,
        IEnumerable<(string User, AccountMemberRole Role)> rows)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(accountMemberService);
        ArgumentNullException.ThrowIfNull(contentManager);
        ArgumentNullException.ThrowIfNull(part);

        var accountId = part.ContentItem?.ContentItemId;
        var resolution = new AccountMemberResolution();

        foreach (var row in (rows ?? []).Where(row => !string.IsNullOrWhiteSpace(row.User)))
        {
            var name = row.User.Trim();
            var user = await userManager.FindByEmailAsync(name) ?? await userManager.FindByNameAsync(name);

            if (user is null)
            {
                resolution.Problems.Add(new AccountMemberProblem(name, AccountMemberProblemKind.UnknownUser, null));

                continue;
            }

            var userId = await userManager.GetUserIdAsync(user);

            if (resolution.Members.Any(member => member.UserId == userId))
            {
                continue;
            }

            if (await accountMemberService.FindOtherAccountIdAsync(userId, accountId) is { } otherId)
            {
                var other = await contentManager.GetAsync(otherId, VersionOptions.Latest);

                resolution.Problems.Add(new AccountMemberProblem(name, AccountMemberProblemKind.OtherAccount, other?.DisplayText ?? otherId));

                continue;
            }

            resolution.Members.Add(new AccountMember
            {
                UserId = userId,
                UserName = await userManager.GetUserNameAsync(user),
                Email = await userManager.GetEmailAsync(user),
                Role = row.Role,
                ContactContentItemId = part.FindMember(userId)?.ContactContentItemId,
            });
        }

        return resolution;
    }
}

/// <summary>
/// The members resolved from the rows typed on an account.
/// </summary>
internal sealed class AccountMemberResolution
{
    /// <summary>
    /// Gets the members.
    /// </summary>
    public List<AccountMember> Members { get; } = [];

    /// <summary>
    /// Gets what is wrong with the rows that could not be resolved. The members are saved only when it is empty.
    /// </summary>
    public List<AccountMemberProblem> Problems { get; } = [];
}

/// <summary>
/// A row of the members that could not be resolved.
/// </summary>
/// <param name="User">The user name or email as typed.</param>
/// <param name="Kind">What is wrong.</param>
/// <param name="OtherAccountName">The account the user already belongs to, for <see cref="AccountMemberProblemKind.OtherAccount"/>.</param>
internal sealed record AccountMemberProblem(string User, AccountMemberProblemKind Kind, string OtherAccountName);

/// <summary>
/// What is wrong with a row of the members.
/// </summary>
internal enum AccountMemberProblemKind
{
    /// <summary>
    /// No user has that user name or email.
    /// </summary>
    UnknownUser,

    /// <summary>
    /// The user is already a member of another account.
    /// </summary>
    OtherAccount,
}
