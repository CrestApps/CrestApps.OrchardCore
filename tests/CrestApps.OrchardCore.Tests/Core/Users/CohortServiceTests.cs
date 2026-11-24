using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Core.Cohorts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Core.Users;

/// <summary>
/// Cohorts on a real SQLite store: names are unique without case, members join and leave once, the observers hear only
/// the people who actually joined or left, a cohort something uses cannot be deleted, and the "Cohort member" audience
/// rule finds the members.
/// </summary>
public sealed class CohortServiceTests : IAsyncLifetime
{
    private static readonly string[] _users = ["ana", "ben", "cid", "dee"];

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"cohorts-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new CohortIndexProvider(), new CohortMemberIndexProvider()], UsersConstants.CohortsCollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(UsersConstants.CohortsCollectionName, cancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(cancellationToken);

        await CohortsIndexSchema.CreateAsync(new SchemaBuilder(_store.Configuration, transaction));
        await transaction.CommitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task CreateAsync_RefusesAnEmptyName_AndANameAnotherCohortHas_WithoutCase()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        await using var session = _store.CreateSession();
        var service = Service(session);

        // Act
        var created = await service.CreateAsync("2026 New Hires", "Starters", cancellationToken: ct);
        var duplicate = await service.CreateAsync(" 2026 new hires ", null, cancellationToken: ct);
        var empty = await service.CreateAsync("  ", null, cancellationToken: ct);
        var found = await service.FindByNameAsync("2026 NEW HIRES", ct);

        // Assert
        Assert.True(created.Succeeded);
        Assert.False(string.IsNullOrEmpty(created.Cohort.CohortId));
        Assert.False(duplicate.Succeeded);
        Assert.False(empty.Succeeded);
        Assert.Equal(created.Cohort.CohortId, found?.CohortId);
    }

    [Fact]
    public async Task UpdateAsync_Renames_ButRefusesTheNameOfAnotherCohort()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        await using var session = _store.CreateSession();
        var service = Service(session);
        var first = (await service.CreateAsync("Supervisors", null, cancellationToken: ct)).Cohort;

        await service.CreateAsync("Drivers", null, cancellationToken: ct);

        // Act
        var renamed = await service.UpdateAsync(first.CohortId, "Shift supervisors", "Every shift", ct);
        var clash = await service.UpdateAsync(first.CohortId, "drivers", null, ct);
        var page = await service.PageAsync("shift", 0, 10, ct);

        // Assert
        Assert.True(renamed.Succeeded);
        Assert.False(clash.Succeeded);
        var cohort = Assert.Single(page.Items);
        Assert.Equal("Shift supervisors", cohort.Name);
        Assert.Equal("Every shift", cohort.Description);
        Assert.Equal(1, page.Count);
    }

    [Fact]
    public async Task Members_JoinAndLeaveOnce_AndTheObserversHearOnlyTheChanges()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var observer = new RecordingObserver();

        await using var session = _store.CreateSession();
        var service = Service(session, observer);
        var cohort = (await service.CreateAsync("Warehouse", null, cancellationToken: ct)).Cohort;
        var other = (await service.CreateAsync("Office", null, cancellationToken: ct)).Cohort;

        // Act
        var firstAdd = await service.AddMembersAsync(cohort.CohortId, ["ana", "ben", "ghost"], "admin", ct);
        var secondAdd = await service.AddMembersAsync(cohort.CohortId, ["ben", "cid", "cid"], "admin", ct);
        await service.AddMembersAsync(other.CohortId, ["ana"], "admin", ct);
        var removed = await service.RemoveMembersAsync(cohort.CohortId, ["ben", "dee"], "admin", ct);
        var page = await service.PageMembersAsync(cohort.CohortId, 0, 10, ct);
        var anaCohorts = await service.ListForUserAsync("ana", ct);

        // Assert
        Assert.Equal(["ana", "ben"], firstAdd.Order(StringComparer.Ordinal));
        Assert.Equal(["cid"], secondAdd);
        Assert.Equal(["ben"], removed);
        Assert.Equal(2, page.Count);
        Assert.Equal(["ana", "cid"], page.Items.Select(member => member.UserId).Order(StringComparer.Ordinal));
        Assert.Equal(2, await service.CountMembersAsync(cohort.CohortId, ct));
        Assert.True(await service.IsMemberAsync(cohort.CohortId, "cid", ct));
        Assert.False(await service.IsMemberAsync(cohort.CohortId, "ben", ct));
        Assert.Equal(["Office", "Warehouse"], anaCohorts.Select(item => item.Name));
        Assert.Equal(
            ["+Warehouse:ana,ben", "+Warehouse:cid", "+Office:ana", "-Warehouse:ben"],
            observer.Calls);
    }

    [Fact]
    public async Task RemoveFromAllAsync_TakesTheUserOutOfEveryCohort_AndTellsTheObservers()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var observer = new RecordingObserver();

        await using var session = _store.CreateSession();
        var service = Service(session, observer);
        var first = (await service.CreateAsync("A", null, cancellationToken: ct)).Cohort;
        var second = (await service.CreateAsync("B", null, cancellationToken: ct)).Cohort;

        await service.AddMembersAsync(first.CohortId, ["dee", "ana"], "admin", ct);
        await service.AddMembersAsync(second.CohortId, ["dee"], "admin", ct);
        observer.Calls.Clear();

        // Act
        await service.RemoveFromAllAsync("dee", "system", ct);

        // Assert
        Assert.Empty(await service.ListForUserAsync("dee", ct));
        Assert.Equal(["ana"], await service.ListMemberIdsAsync(first.CohortId, ct));
        Assert.Equal(["-A:dee", "-B:dee"], observer.Calls.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task DeleteAsync_RefusesACohortSomethingUses_AndOtherwiseDeletesItWithItsMembers()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var observer = new RecordingObserver();
        var usage = new Mock<ICohortUsage>();

        await using var session = _store.CreateSession();
        var service = Service(session, observer, usage.Object);
        var used = (await service.CreateAsync("Used", null, cancellationToken: ct)).Cohort;
        var free = (await service.CreateAsync("Free", null, cancellationToken: ct)).Cohort;

        usage.Setup(item => item.ListUsesAsync(used.CohortId, It.IsAny<CancellationToken>())).ReturnsAsync(["Course: Safety basics"]);
        usage.Setup(item => item.ListUsesAsync(free.CohortId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await service.AddMembersAsync(free.CohortId, ["ana"], "admin", ct);
        observer.Calls.Clear();

        // Act
        var refused = await service.DeleteAsync(used.CohortId, ct);
        var deleted = await service.DeleteAsync(free.CohortId, ct);

        // Assert
        Assert.NotEmpty(refused);
        Assert.Empty(deleted);
        Assert.NotNull(await service.FindByIdAsync(used.CohortId, ct));
        Assert.Null(await service.FindByIdAsync(free.CohortId, ct));
        Assert.Empty(await service.ListForUserAsync("ana", ct));
        Assert.Empty(observer.Calls);
    }

    [Fact]
    public async Task CohortMemberRule_FindsTheMembersOfTheChosenCohorts()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        await using var session = _store.CreateSession();
        var service = Service(session);
        var first = (await service.CreateAsync("Morning", null, cancellationToken: ct)).Cohort;
        var second = (await service.CreateAsync("Evening", null, cancellationToken: ct)).Cohort;

        await service.AddMembersAsync(first.CohortId, ["ana", "ben"], "admin", ct);
        await service.AddMembersAsync(second.CohortId, ["cid"], "admin", ct);

        var source = new CohortUserAudienceRuleSource(service, new Mock<IStringLocalizer<CohortUserAudienceRuleSource>>().Object);
        var rule = new UserAudienceRule { Source = CohortUserAudienceRuleSource.SourceName, Values = [first.CohortId, second.CohortId] };

        // Act
        var members = await source.ResolveUserIdsAsync(rule, ct);
        var options = await source.GetOptionsAsync(ct);
        var cid = await source.IsMatchAsync(new UserAudienceRule { Source = rule.Source, Values = [first.CohortId] }, "cid", ct);
        var ana = await source.IsMatchAsync(new UserAudienceRule { Source = rule.Source, Values = [first.CohortId] }, "ana", ct);

        // Assert
        Assert.Equal(["ana", "ben", "cid"], members.Order(StringComparer.Ordinal));
        Assert.Equal(["Evening", "Morning"], options.Select(option => option.Text));
        Assert.False(cid);
        Assert.True(ana);
    }

    private static DefaultCohortService Service(ISession session, ICohortMembershipObserver observer = null, ICohortUsage usage = null)
    {
        var userManager = new Mock<UserManager<IUser>>(new Mock<IUserStore<IUser>>().Object, null, null, null, null, null, null, null, null);

        userManager.Setup(manager => manager.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => _users.Contains(id) ? new User { UserId = id, UserName = id } : null);

        var clock = new Mock<IClock>();

        clock.SetupGet(item => item.UtcNow).Returns(() => DateTime.UtcNow);

        return new DefaultCohortService(
            session,
            observer is null ? [] : [observer],
            usage is null ? [] : [usage],
            userManager.Object,
            clock.Object,
            new Mock<IStringLocalizer<DefaultCohortService>>().Object);
    }

    private sealed class RecordingObserver : ICohortMembershipObserver
    {
        public List<string> Calls { get; } = [];

        public Task MembersAddedAsync(Cohort cohort, IReadOnlyList<string> userIds, string actor, CancellationToken cancellationToken = default)
        {
            Calls.Add($"+{cohort.Name}:{string.Join(',', userIds.Order(StringComparer.Ordinal))}");

            return Task.CompletedTask;
        }

        public Task MembersRemovedAsync(Cohort cohort, IReadOnlyList<string> userIds, string actor, CancellationToken cancellationToken = default)
        {
            Calls.Add($"-{cohort.Name}:{string.Join(',', userIds.Order(StringComparer.Ordinal))}");

            return Task.CompletedTask;
        }
    }
}
