using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Scoring;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Surveys.Scoring;

public sealed class SurveyOverrideServiceTests : IAsyncLifetime
{
    private static readonly DateTime _now = new(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);

    private readonly ScoringDispatcher _dispatcher = new();
    private ScoringStore _store;

    public async ValueTask InitializeAsync()
        => _store = await ScoringStore.CreateAsync();

    public async ValueTask DisposeAsync()
        => await _store.DisposeAsync();

    [Fact]
    public async Task GrantAsync_RequiresAReasonAndSomeoneToApplyTo()
    {
        // Act and assert
        Assert.Equal(SurveyErrorCode.InvalidAnswer, await RunAsync(service => service.GrantAsync(new SurveyOverride { SurveyId = "s1", UserId = "u1", ExtraMinutes = 10 })));
        Assert.Equal(SurveyErrorCode.InvalidAnswer, await RunAsync(service => service.GrantAsync(new SurveyOverride { SurveyId = "s1", Reason = "Medical", ExtraMinutes = 10 })));
        Assert.Empty(_dispatcher.Events);
    }

    [Fact]
    public async Task FindEffectiveAsync_CombinesTheMostGenerousTerms()
    {
        // Arrange
        await RunAsync(service => service.GrantAsync(new SurveyOverride { SurveyId = "s1", UserId = "u1", ExtraMinutes = 15, ExtraAttempts = 1, Reason = "Medical" }));
        await RunAsync(service => service.GrantAsync(new SurveyOverride { SurveyId = "s1", AudienceId = "extra-time", ExtraMinutes = 30, ClosesUtc = _now.AddDays(3), Reason = "Policy" }));
        await RunAsync(service => service.GrantAsync(new SurveyOverride { SurveyId = "s1", UserId = "u2", ExtraAttempts = 5, Reason = "Other person" }));

        // Act
        var effective = await RunAsync(service => service.FindEffectiveAsync("s1", "u1", null));

        // Assert
        Assert.Equal(30, effective.ExtraMinutes);
        Assert.Equal(1, effective.ExtraAttempts);
        Assert.Equal(_now.AddDays(3), effective.ClosesUtc);
        Assert.Equal(["OverrideGranted", "OverrideGranted", "OverrideGranted"], _dispatcher.Events);
    }

    [Fact]
    public async Task FindEffectiveAsync_KeepsAContextScopedAccommodationToItsContext()
    {
        // Arrange
        var enrollment = new RecordRef("LearningEnrollment", "e1");
        await RunAsync(service => service.GrantAsync(new SurveyOverride { SurveyId = "s1", UserId = "u1", ExtraMinutes = 15, Context = enrollment, Reason = "Course" }));

        // Act and assert
        Assert.Equal(15, (await RunAsync(service => service.FindEffectiveAsync("s1", "u1", enrollment))).ExtraMinutes);
        Assert.Null(await RunAsync(service => service.FindEffectiveAsync("s1", "u1", new RecordRef("LearningEnrollment", "e2"))));
        Assert.Null(await RunAsync(service => service.FindEffectiveAsync("s1", "u1", null)));
    }

    [Fact]
    public async Task ListForUserAsync_ReadsOnlyTheUsersOwnAndAudienceAccommodations_OfThatSurvey()
    {
        // Arrange
        await RunAsync(service => service.GrantAsync(new SurveyOverride { SurveyId = "s1", UserId = "u1", ExtraMinutes = 15, Reason = "Mine" }));
        await RunAsync(service => service.GrantAsync(new SurveyOverride { SurveyId = "s1", AudienceId = "extra-time", ExtraMinutes = 30, Reason = "Audience" }));
        await RunAsync(service => service.GrantAsync(new SurveyOverride { SurveyId = "s1", UserId = "u2", ExtraMinutes = 45, Reason = "Theirs" }));
        await RunAsync(service => service.GrantAsync(new SurveyOverride { SurveyId = "s2", UserId = "u1", ExtraMinutes = 60, Reason = "Other survey" }));

        // Act
        await using var session = _store.Store.CreateSession();
        var store = new SurveyOverrideStore(session);
        var mine = await store.ListForUserAsync("s1", "u1", TestContext.Current.CancellationToken);
        var guest = await store.ListForUserAsync("s1", null, TestContext.Current.CancellationToken);

        // Assert: the runner never reads another person's accommodation, and a guest reads none.
        Assert.Equal(["Audience", "Mine"], mine.Select(item => item.Reason).Order(StringComparer.Ordinal));
        Assert.Empty(guest);
        Assert.Null(await RunAsync(service => service.FindEffectiveAsync("s1", null, null)));
        Assert.Equal(30, (await RunAsync(service => service.FindEffectiveAsync("s1", "u1", null))).ExtraMinutes);
    }

    [Fact]
    public async Task RevokeAsync_RemovesTheAccommodation()
    {
        // Arrange
        var granted = new SurveyOverride { SurveyId = "s1", UserId = "u1", ExtraMinutes = 15, Reason = "Medical" };
        await RunAsync(service => service.GrantAsync(granted));

        // Act
        var revoked = await RunAsync(service => service.RevokeAsync(granted.ItemId));

        // Assert
        Assert.True(revoked);
        Assert.Empty(await RunAsync(service => service.ListAsync("s1")));
    }

    private async Task<T> RunAsync<T>(Func<SurveyOverrideService, Task<T>> operation)
    {
        await using var session = _store.Store.CreateSession();
        var service = new SurveyOverrideService(new SurveyOverrideStore(session), [new Audiences()], _dispatcher, new TestClock(_now));
        var result = await operation(service);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return result;
    }

    private sealed class Audiences : IUserAudienceService
    {
        public Task<IReadOnlyList<UserAudienceSummary>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserAudienceSummary>>([]);

        public Task<UserAudienceDefinition> FindDefinitionAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult<UserAudienceDefinition>(null);

        public Task<IReadOnlyList<string>> SaveDefinitionAsync(UserAudienceDefinition definition, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<bool> DeleteDefinitionAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<(int Count, IReadOnlyList<string> UserIds)> GetMembersAsync(string name, int skip, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<(int, IReadOnlyList<string>)>((0, []));

        public Task<bool> IsMemberAsync(string name, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(name == "extra-time" && userId == "u1");
    }
}
