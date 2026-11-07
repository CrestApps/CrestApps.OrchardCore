using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Memberships.Deployments;
using CrestApps.OrchardCore.Memberships.Models;
using CrestApps.OrchardCore.Memberships.Recipes;
using CrestApps.OrchardCore.Memberships.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Users;
using Moq;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Tests.Memberships;

/// <summary>
/// Memberships travel between sites with their member by user name: an export imports into an empty store with the same
/// ids, a second run matches instead of duplicating, the member's id on the importing site is used, and an active
/// membership grants its roles there.
/// </summary>
public sealed class MembershipsPortabilityTests
{
    private static readonly DateTime _starts = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Memberships_ExportAndImportIntoAnEmptyStore_AndGrantTheirRoles()
    {
        // Arrange
        var source = new InMemoryMembershipStore();

        source.Entries.Add(new Membership
        {
            ItemId = "4ms00000000000000000000001",
            UserId = "source-user-id",
            UserName = "ana",
            ContentItemId = "gold-product",
            Title = "Gold",
            RoleNames = ["Gold member"],
            AddedRoleNames = ["Gold member"],
            OrderIds = ["order-1"],
            StartsUtc = _starts,
            Status = MembershipStatus.Active,
        });

        var target = new InMemoryMembershipStore();
        var users = new Mock<IUserService>();
        var grants = new Mock<IRoleGrantService>();

        users.Setup(value => value.GetUserAsync("ana")).ReturnsAsync(new User { UserId = "target-user-id", UserName = "ana" });

        var step = new MembershipsStep(target, users.Object, grants.Object, RecipeRoundTrip.Localizer<MembershipsStep>());

        // Act
        var exported = await RecipeRoundTrip.ExportAsync(new MembershipsDeploymentSource(source), new MembershipsDeploymentStep());
        var first = await RecipeRoundTrip.RunAsync(step, exported);
        var second = await RecipeRoundTrip.RunAsync(step, exported);

        // Assert
        var json = exported.ToJsonString();

        Assert.Equal("Memberships", exported["name"].GetValue<string>());
        Assert.DoesNotContain("source-user-id", json);
        Assert.False(exported["Memberships"][0].AsObject().ContainsKey("AddedRoleNames"));
        Assert.Empty(first);
        Assert.Empty(second);

        var membership = Assert.Single(target.Entries);

        Assert.Equal("4ms00000000000000000000001", membership.ItemId);
        Assert.Equal("target-user-id", membership.UserId);
        Assert.Equal(["Gold member"], membership.RoleNames);
        Assert.Empty(membership.AddedRoleNames);
        Assert.Equal(_starts, membership.StartsUtc);
        grants.Verify(value => value.GrantAsync("target-user-id", "Gold member", MembershipRoleGrantSource.SourceName, membership.ItemId), Times.Exactly(2));
    }

    [Fact]
    public async Task Memberships_ReportAMemberThatDoesNotExist()
    {
        // Arrange
        var target = new InMemoryMembershipStore();
        var step = new MembershipsStep(target, Mock.Of<IUserService>(), Mock.Of<IRoleGrantService>(), RecipeRoundTrip.Localizer<MembershipsStep>());
        var recipe = JsonNode.Parse("""
            { "name": "Memberships", "Memberships": [ { "UserName": "nobody", "ContentItemId": "gold-product", "StartsUtc": "2026-01-01T00:00:00Z" } ] }
            """).AsObject();

        // Act
        var errors = await RecipeRoundTrip.RunAsync(step, recipe);

        // Assert
        Assert.Equal(["There's no user named nobody."], errors);
        Assert.Empty(target.Entries);
    }

    private sealed class InMemoryMembershipStore : InMemoryCatalog<Membership>, IMembershipStore
    {
        public Task<IReadOnlyList<Membership>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Membership>>(Entries.Where(entry => entry.UserId == userId).ToList());

        public Task<IReadOnlyList<Membership>> ListByOrderAsync(string userId, string orderId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Membership>>(Entries.Where(entry => entry.UserId == userId && entry.OrderIds.Contains(orderId)).ToList());

        public Task<IReadOnlyList<Membership>> ListEndedAsync(DateTime utcNow, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Membership>>([]);

        public Task<(int Count, IReadOnlyList<Membership> Memberships)> PageAsync(string search, MembershipStatus? status, int skip, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<(int, IReadOnlyList<Membership>)>((Entries.Count, Entries.ToList()));
    }
}
