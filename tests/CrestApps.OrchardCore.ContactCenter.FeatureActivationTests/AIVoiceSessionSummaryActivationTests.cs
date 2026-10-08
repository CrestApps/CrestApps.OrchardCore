using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

/// <summary>
/// The per-call AI voice session summaries on a real tenant: the table the usage analytics feature creates, the
/// store that fills it, and the writer the voice loop hands each call to.
/// </summary>
public sealed class AIVoiceSessionSummaryActivationTests
{
    [Fact]
    public async Task WithUsageAnalytics_ASummaryIsStored_AndReadBackForTheReport()
    {
        // Arrange
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(Profile("voice-usage-analytics", AIConstants.Feature.ChatAnalytics));
        var started = new DateTime(2026, 9, 24, 15, 0, 0, 123, DateTimeKind.Utc);

        // Act
        await host.ExecuteInTenantScopeAsync(tenant, services => services.GetRequiredService<IAIVoiceSessionSummaryStore>().SaveAsync(new AIVoiceSessionSummary
        {
            ItemId = "summary-1",
            ActivityId = "activity-1",
            AIProfileId = "profile-1",
            Engine = AIVoiceSessionEngine.Realtime,
            Outcome = AIVoiceSessionOutcome.HandedToAgent,
            DeploymentName = "voice-live",
            StartedUtc = started,
            EndedUtc = started.AddSeconds(90),
            CreatedUtc = started.AddSeconds(90),
            SessionDurationMs = 90_000,
            AssistantSpeakingMs = 40_000,
            BargeIns = 2,
        }, TestContext.Current.CancellationToken));

        var (rows, found) = await host.ExecuteInTenantScopeAsync(tenant, async services =>
        {
            var store = services.GetRequiredService<IAIVoiceSessionSummaryStore>();

            return (
                await store.GetAsync(started.AddDays(-1), started, TestContext.Current.CancellationToken),
                await store.FindByActivityAsync("activity-1", TestContext.Current.CancellationToken));
        });

        // Assert
        Assert.IsNotType<NullAIVoiceSessionSummaryStore>(await host.ExecuteInTenantScopeAsync(tenant, services =>
            Task.FromResult(services.GetRequiredService<IAIVoiceSessionSummaryStore>())));

        var row = Assert.Single(rows);
        Assert.Equal("activity-1", row.ActivityId);
        Assert.Equal(nameof(AIVoiceSessionEngine.Realtime), row.Engine);
        Assert.Equal(nameof(AIVoiceSessionOutcome.HandedToAgent), row.Outcome);
        Assert.Equal(90_000, row.SessionDurationMs);
        Assert.Equal(2, row.BargeIns);
        Assert.Null(row.CallerSpeakingMs);

        // The document keeps every tick, which is what the durations are checked against.
        Assert.Equal(started, found.StartedUtc);
    }

    [Fact]
    public async Task WithAutomatedVoice_TheWriterTheLoopHandsCallsTo_Resolves()
    {
        // Arrange
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(Profile("voice-usage-writer", AIConstants.Feature.ChatAnalytics));

        // Act
        var resolved = await host.ExecuteInTenantScopeAsync(tenant, services =>
        {
            var writer = typeof(IAIVoiceSessionTracker).Assembly
                .GetType("CrestApps.OrchardCore.Omnichannel.Voice.Services.AIVoiceSessionSummaryWriter", throwOnError: true);

            return Task.FromResult(
                services.GetRequiredService(writer) is not null &&
                services.GetRequiredService<IAIVoiceSessionTracker>() is not null);
        });

        // Assert
        Assert.True(resolved);
    }

    private static ContactCenterTenantProfile Profile(string id, params string[] features)
        => new()
        {
            Id = id,
            ProviderProfile = "none",
            Features = [OmnichannelVoiceConstants.Feature.Area, .. features],
        };
}