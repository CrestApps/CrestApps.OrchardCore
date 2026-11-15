using CrestApps.OrchardCore.Tests.AgenticCommerce;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Core.Audiences;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Core.Users;

/// <summary>
/// Audiences administrators define: rules joined by all keep the people every rule matches, rules joined by any keep
/// the people one matches, membership checks agree with the member list, and a definition with no rules, an unknown
/// rule or the name of a module audience is refused.
/// </summary>
public sealed class UserAudienceServiceTests
{
    [Fact]
    public async Task GetMembersAsync_IntersectsForAll_AndUnitesForAny_AndIsMemberAgrees()
    {
        // Arrange
        var service = Service();
        var ct = TestContext.Current.CancellationToken;
        var rules = new List<UserAudienceRule>
        {
            new() { Source = "Team", Values = ["field"] },
            new() { Source = "Hired", Number = 30 },
        };

        Assert.Empty(await service.SaveDefinitionAsync(new UserAudienceDefinition { Name = "field-veterans", DisplayName = "Field veterans", Join = UserAudienceJoin.All, Rules = rules }, ct));
        Assert.Empty(await service.SaveDefinitionAsync(new UserAudienceDefinition { Name = "field-or-veterans", Join = UserAudienceJoin.Any, Rules = rules }, ct));

        // Act
        var all = await service.GetMembersAsync("field-veterans", 0, 10, ct);
        var any = await service.GetMembersAsync("field-or-veterans", 0, 2, ct);
        var ana = await service.IsMemberAsync("field-veterans", "ana", ct);
        var ben = await service.IsMemberAsync("field-veterans", "ben", ct);
        var benAny = await service.IsMemberAsync("field-or-veterans", "ben", ct);
        var nobody = await service.IsMemberAsync("field-or-veterans", "zoe", ct);
        var listed = await service.ListAsync(ct);

        // Assert
        Assert.Equal(["ana"], all.UserIds);
        Assert.Equal(1, all.Count);
        Assert.Equal(3, any.Count);
        Assert.Equal(["ana", "ben"], any.UserIds);
        Assert.True(ana);
        Assert.False(ben);
        Assert.True(benAny);
        Assert.False(nobody);
        Assert.Contains(listed, audience => audience.Name == "field-veterans" && audience.DisplayName == "Field veterans" && audience.IsDefined);
        Assert.Contains(listed, audience => audience.Name == "Staff" && !audience.IsDefined);
    }

    [Fact]
    public async Task SaveDefinitionAsync_RefusesNoRules_AnUnknownRule_AModuleAudienceName_AndABadName()
    {
        // Arrange
        var service = Service();
        var ct = TestContext.Current.CancellationToken;

        // Act
        var empty = await service.SaveDefinitionAsync(new UserAudienceDefinition { Name = "empty" }, ct);
        var unknown = await service.SaveDefinitionAsync(new UserAudienceDefinition { Name = "unknown", Rules = [new() { Source = "Nope" }] }, ct);
        var clash = await service.SaveDefinitionAsync(new UserAudienceDefinition { Name = "staff", Rules = [new() { Source = "Team", Values = ["field"] }] }, ct);
        var badName = await service.SaveDefinitionAsync(new UserAudienceDefinition { Name = "has spaces", Rules = [new() { Source = "Team", Values = ["field"] }] }, ct);
        var deleted = await service.DeleteDefinitionAsync("empty", ct);

        // Assert
        Assert.NotEmpty(empty);
        Assert.NotEmpty(unknown);
        Assert.NotEmpty(clash);
        Assert.NotEmpty(badName);
        Assert.False(deleted);
        Assert.DoesNotContain(await service.ListAsync(ct), audience => audience.IsDefined);
    }

    private static DefaultUserAudienceService Service()
    {
        var userManager = new Mock<UserManager<IUser>>(new Mock<IUserStore<IUser>>().Object, null, null, null, null, null, null, null, null);
        var staff = new Mock<IUserAudienceProvider>();

        staff.SetupGet(provider => provider.Name).Returns("Staff");

        return new DefaultUserAudienceService(
            new InMemoryDocumentManager<UserAudiencesDocument>(),
            [staff.Object],
            [new FixedSource("Team", "ana", "ben"), new FixedSource("Hired", "ana", "cid")],
            userManager.Object,
            new Mock<IStringLocalizer<DefaultUserAudienceService>>().Object);
    }

    private sealed class FixedSource : IUserAudienceRuleSource
    {
        private readonly string[] _userIds;

        public FixedSource(string name, params string[] userIds)
        {
            Name = name;
            _userIds = userIds;
        }

        public string Name { get; }

        public LocalizedString DisplayName => new(Name, Name);

        public Task<IReadOnlyList<UserAudienceRuleOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserAudienceRuleOption>>([]);

        public Task<IReadOnlyCollection<string>> ResolveUserIdsAsync(UserAudienceRule rule, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<string>>(_userIds);
    }
}
