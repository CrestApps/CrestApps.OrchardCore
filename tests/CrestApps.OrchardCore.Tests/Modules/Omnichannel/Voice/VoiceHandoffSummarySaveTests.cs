using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// Saving the summary the agent a call was handed to reads.
/// </summary>
/// <remarks>
/// On the AI-0 walk the summary of a call handed to an agent was lost: the model took seconds to write it, routing wrote
/// the activity meanwhile, and the save onto the copy read before the model was asked lost that race and was dropped.
/// </remarks>
public sealed class VoiceHandoffSummarySaveTests
{
    private static readonly DateTime _now = new(2026, 10, 28, 15, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SaveHandoffSummaryAsync_OnAFreshRead_SetsTheSummaryAndSavesIt()
    {
        // Arrange
        var harness = new SaveHarness();

        // Act
        await harness.SaveAsync("The customer wants a technician for a leaking boiler.");

        // Assert
        var saved = Assert.Single(harness.Saved);
        Assert.Equal("The customer wants a technician for a leaking boiler.", saved.HandoffSummary);
        Assert.Equal(_now, saved.HandoffSummaryUtc);
        Assert.Equal(1, harness.Scopes);
    }

    [Fact]
    public async Task SaveHandoffSummaryAsync_WhenAnotherWriteWinsTheSave_SavesOnANewReadInANewScope()
    {
        // Arrange
        var harness = new SaveHarness(conflictsBeforeSuccess: 1);

        // Act
        await harness.SaveAsync("The customer asked for a person.");

        // Assert
        Assert.Equal(2, harness.Scopes);
        Assert.Equal(2, harness.Reads);
        Assert.Equal("The customer asked for a person.", harness.Saved[^1].HandoffSummary);
        Assert.Equal("routing's write", harness.Saved[^1].Notes);
    }

    [Fact]
    public async Task SaveHandoffSummaryAsync_WhenTheActivityIsAlreadySummarized_LeavesItAlone()
    {
        // Arrange
        var harness = new SaveHarness(existingSummary: "Written by an earlier delivery.");

        // Act
        await harness.SaveAsync("A second summary.");

        // Assert
        Assert.Empty(harness.Saved);
        Assert.Equal(1, harness.Scopes);
    }

    [Fact]
    public async Task SaveHandoffSummaryAsync_WhenEveryAttemptLoses_GivesUpAfterThreeWithoutThrowing()
    {
        // Arrange
        var harness = new SaveHarness(conflictsBeforeSuccess: int.MaxValue);

        // Act
        await harness.SaveAsync("Never saved.");

        // Assert
        Assert.Equal(3, harness.Scopes);
    }

    private sealed class SaveHarness
    {
        private readonly string _existingSummary;
        private int _conflictsLeft;

        public SaveHarness(int conflictsBeforeSuccess = 0, string existingSummary = null)
        {
            _conflictsLeft = conflictsBeforeSuccess;
            _existingSummary = existingSummary;
        }

        public List<OmnichannelActivity> Saved { get; } = [];

        public int Scopes { get; private set; }

        public int Reads { get; private set; }

        public Task SaveAsync(string summary)
        {
            return VoiceAgentConversationLoop.SaveHandoffSummaryAsync(
                InANewScopeAsync,
                "activity-1",
                summary,
                NullLogger.Instance);
        }

        // Each attempt gets services of its own, as a child shell scope would: a fresh read of the activity, and a
        // session that commits it or loses to another write.
        private async Task InANewScopeAsync(Func<IServiceProvider, Task> work)
        {
            Scopes++;
            OmnichannelActivity updated = null;

            var store = new Mock<IOmnichannelActivityStore>();
            store.Setup(x => x.FindByIdAsync("activity-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    Reads++;

                    // The second read carries what routing wrote after the first.
                    return new OmnichannelActivity
                    {
                        ItemId = "activity-1",
                        HandoffSummary = _existingSummary,
                        Notes = Reads > 1 ? "routing's write" : null,
                    };
                });
            store.Setup(x => x.UpdateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<CancellationToken>()))
                .Callback<OmnichannelActivity, CancellationToken>((activity, _) => updated = activity);

            var session = new Mock<ISession>();
            session.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (_conflictsLeft > 0)
                    {
                        _conflictsLeft--;

                        return Task.FromException(new ConcurrencyException(new Document()));
                    }

                    Saved.Add(updated);

                    return Task.CompletedTask;
                });

            await using var services = new ServiceCollection()
                .AddSingleton(store.Object)
                .AddSingleton(session.Object)
                .AddSingleton<IClock>(new StubClock(_now))
                .BuildServiceProvider();

            await work(services);
        }
    }
}
