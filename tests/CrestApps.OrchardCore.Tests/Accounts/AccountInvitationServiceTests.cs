using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Accounts.Core.Indexes;
using CrestApps.OrchardCore.Accounts.Core.Services;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Learning.Assessments;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Accounts;

/// <summary>
/// Account invitations: only the newest link to an address works, the person who signs in with the invited address
/// becomes a member with the invited role, another address, another account, an expired or a taken-back link are
/// refused, and the token itself is never stored.
/// </summary>
public sealed class AccountInvitationServiceTests
{
    [Fact]
    public async Task AcceptAsync_MakesThePersonWithTheInvitedAddressAMember_AndRefusesEveryoneElse()
    {
        // Arrange
        await using var h = await Harness.CreateAsync();

        var first = await h.RunAsync(services => services.GetRequiredService<IAccountInvitationService>().InviteAsync(new AccountInvitationRequest { AccountId = "acme", Email = "Ana@Example.com", Role = AccountMemberRole.Admin, InvitedBy = "boss" }));
        var second = await h.RunAsync(services => services.GetRequiredService<IAccountInvitationService>().InviteAsync(new AccountInvitationRequest { AccountId = "acme", Email = "ana@example.com", Role = AccountMemberRole.Admin, InvitedBy = "boss" }));

        // Act
        var oldLink = await h.RunAsync(services => services.GetRequiredService<IAccountInvitationService>().AcceptAsync(first.Token, "ana"));
        var wrongPerson = await h.RunAsync(services => services.GetRequiredService<IAccountInvitationService>().AcceptAsync(second.Token, "ben"));
        var accepted = await h.RunAsync(services => services.GetRequiredService<IAccountInvitationService>().AcceptAsync(second.Token, "ana"));
        var acceptedAgain = await h.RunAsync(services => services.GetRequiredService<IAccountInvitationService>().AcceptAsync(second.Token, "ana"));
        var memberAgain = await h.RunAsync(services => services.GetRequiredService<IAccountInvitationService>().InviteAsync(new AccountInvitationRequest { AccountId = "acme", Email = "ana@example.com" }));
        var stored = await h.RunAsync(services => services.GetRequiredService<IAccountInvitationService>().ListForAccountAsync("acme"));

        // Assert
        Assert.True(first.Succeeded);
        Assert.False(oldLink.Succeeded);
        Assert.False(wrongPerson.Succeeded);
        Assert.True(accepted.Succeeded, accepted.Error);
        Assert.True(acceptedAgain.Succeeded);
        Assert.False(memberAgain.Succeeded);
        var member = Assert.Single(h.Members);
        Assert.Equal("ana", member.UserId);
        Assert.Equal(AccountMemberRole.Admin, member.Role);
        Assert.Contains(h.Observer, value => value == "accepted:ana@example.com");
        Assert.Equal([AccountInvitationState.Accepted, AccountInvitationState.Revoked], stored.Select(invitation => invitation.State).Order());
        Assert.DoesNotContain(stored, invitation => invitation.TokenHash == second.Token);
        Assert.Equal(DefaultAccountInvitationService.Hash(second.Token), stored.Single(invitation => invitation.State == AccountInvitationState.Accepted).TokenHash);
    }

    [Fact]
    public async Task AcceptAsync_RefusesAnExpiredOrRevokedLink_AndAPersonOfAnotherAccount()
    {
        // Arrange
        await using var h = await Harness.CreateAsync();

        var expiring = await h.RunAsync(services => services.GetRequiredService<IAccountInvitationService>().InviteAsync(new AccountInvitationRequest { AccountId = "acme", Email = "cid@example.com", Lifetime = TimeSpan.FromDays(1) }));
        var revoked = await h.RunAsync(services => services.GetRequiredService<IAccountInvitationService>().InviteAsync(new AccountInvitationRequest { AccountId = "acme", Email = "dee@example.com" }));
        var other = await h.RunAsync(services => services.GetRequiredService<IAccountInvitationService>().InviteAsync(new AccountInvitationRequest { AccountId = "acme", Email = "eve@example.com" }));
        var badEmail = await h.RunAsync(services => services.GetRequiredService<IAccountInvitationService>().InviteAsync(new AccountInvitationRequest { AccountId = "acme", Email = "not-an-address" }));

        await h.RunAsync(services => services.GetRequiredService<IAccountInvitationService>().RevokeAsync(revoked.Invitation.ItemId, "boss"));
        h.Clock.UtcNow = h.Clock.UtcNow.AddDays(2);

        // Act
        var late = await h.RunAsync(services => services.GetRequiredService<IAccountInvitationService>().AcceptAsync(expiring.Token, "cid"));
        var taken = await h.RunAsync(services => services.GetRequiredService<IAccountInvitationService>().AcceptAsync(revoked.Token, "dee"));
        var elsewhere = await h.RunAsync(services => services.GetRequiredService<IAccountInvitationService>().AcceptAsync(other.Token, "eve"));

        // Assert
        Assert.False(badEmail.Succeeded);
        Assert.False(late.Succeeded);
        Assert.False(taken.Succeeded);
        Assert.False(elsewhere.Succeeded);
        Assert.Empty(h.Members);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private CalendarTestHarness _inner;

        public ContentItem Account { get; } = new() { ContentItemId = "acme", ContentType = "Account", Latest = true, Published = true };

        public TestClock Clock { get; } = new() { UtcNow = CalendarTestHarness.Now };

        public List<AccountMember> Members => Account.TryGet<AccountMembersPart>(out var part) ? part.Members : [];

        public List<string> Observer { get; } = [];

        public static async Task<Harness> CreateAsync()
        {
            var harness = new Harness();

            harness.Account.Apply(new AccountMembersPart());

            var content = new Mock<IContentManager>();

            content.Setup(manager => manager.GetAsync("acme", It.IsAny<VersionOptions>())).ReturnsAsync(harness.Account);

            var users = new Mock<UserManager<IUser>>(new Mock<IUserStore<IUser>>().Object, null, null, null, null, null, null, null, null);

            foreach (var name in new[] { "ana", "ben", "cid", "dee", "eve" })
            {
                var user = Mock.Of<IUser>(value => value.UserName == name);

                users.Setup(manager => manager.FindByIdAsync(name)).ReturnsAsync(user);
                users.Setup(manager => manager.GetEmailAsync(user)).ReturnsAsync(name + "@example.com");
                users.Setup(manager => manager.GetUserNameAsync(user)).ReturnsAsync(name);
            }

            var members = new Mock<IAccountMemberService>();

            members.Setup(service => service.FindOtherAccountIdAsync("eve", It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("globex");

            harness._inner = await CalendarTestHarness.CreateAsync(
                services =>
                {
                    services.RemoveAll<IClock>();
                    services.AddSingleton<IClock>(harness.Clock);
                    services.AddSingleton(content.Object);
                    services.AddSingleton(users.Object);
                    services.AddSingleton(members.Object);
                    services.AddScoped<AccountInvitationStore>();
                    services.AddScoped<IAccountInvitationService, DefaultAccountInvitationService>();
                    services.AddSingleton<IAccountInvitationObserver>(new RecordingObserver(harness.Observer));
                },
                AccountInvitationIndexSchema.CreateAsync,
                [new AccountInvitationIndexProvider()],
                [AccountsConstants.InvitationsCollectionName]);

            return harness;
        }

        public Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> work) => _inner.RunAsync(work);

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }

    private sealed class RecordingObserver : IAccountInvitationObserver
    {
        private readonly List<string> _events;

        public RecordingObserver(List<string> events)
        {
            _events = events;
        }

        public Task AcceptedAsync(AccountInvitation invitation, CancellationToken cancellationToken = default)
        {
            _events.Add("accepted:" + invitation.Email.ToLowerInvariant());

            return Task.CompletedTask;
        }
    }
}
