using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Users.Core.Services;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Parties;

/// <summary>
/// One published account with its members and their roles, answered through the real
/// <see cref="AccountPartyMemberSource"/> and <see cref="PartyMembershipService"/>, so a test proves what an account's admins
/// and plain members may do end to end.
/// </summary>
internal static class AccountMembershipFixture
{
    /// <summary>
    /// Creates the account members service of one account.
    /// </summary>
    public static IAccountMemberService CreateMembers(string accountId, params (string UserId, AccountMemberRole Role)[] members)
    {
        var account = new ContentItem { ContentItemId = accountId, ContentType = "Account" };
        account.Apply(new AccountMembersPart
        {
            Members = [.. members.Select(member => new AccountMember { UserId = member.UserId, Role = member.Role })],
        });

        var service = new Mock<IAccountMemberService>();
        service
            .Setup(candidate => candidate.GetMemberUserIdsAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. members.Select(member => member.UserId)]);
        service
            .Setup(candidate => candidate.GetMembersAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => account.TryGet<AccountMembersPart>(out var part) ? part.Members : []);
        service
            .Setup(candidate => candidate.FindAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, CancellationToken _) => members.Any(member => member.UserId == userId) ? account : null);

        return service.Object;
    }

    /// <summary>
    /// Creates the party membership of users and of one account.
    /// </summary>
    public static IPartyMembershipService CreateMembership(string accountId, params (string UserId, AccountMemberRole Role)[] members)
        => new PartyMembershipService(
        [
            new UserPartyMemberSource(),
            new AccountPartyMemberSource(CreateMembers(accountId, members)),
        ]);
}
