using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// What a waiting caller actually hears. The policy decides what is due and the provider makes it audible; this
/// is the part in between, which finds the caller's live leg, works out what to say, and records that it was
/// said. Before it, every queue-treatment setting a tenant could configure produced silence.
/// </summary>
public sealed class QueueTreatmentServiceTests
{
    private static readonly DateTime _now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task TheFirstThingACallerHears_IsTheWelcomeAndThenHoldMusic()
    {
        // Arrange
        var harness = new TreatmentHarness();
        harness.WithWaitingCaller("item-1", waitedSeconds: 1);

        // Act
        await harness.RunAsync();

        // Assert
        Assert.Equal("Thanks for calling. We will be with you shortly.", harness.Provider.Spoken.Single());
        Assert.Equal("https://example.test/hold.mp3", harness.Provider.HoldMusic.Single());
    }

    [Fact]
    public async Task AQueueThatOnlyPlaysMusic_IsNotSkippedAsHavingNothingConfigured()
    {
        // Arrange
        // This is the bug a caller actually hit. The sweep skips a queue that has asked for nothing, before it
        // reads anybody waiting in it — and hold music was not counted as asking for something. An operator who
        // set hold music and nothing else got silence, with the media uploaded, the queue configured and the
        // provider perfectly able to play it.
        var harness = new TreatmentHarness();
        harness.Settings.WelcomeMessage = null;
        harness.Settings.AnnouncementIntervalSeconds = 0;
        harness.Settings.CallbackDtmfKey = null;
        harness.WithWaitingCaller("item-1", waitedSeconds: 1);

        // Act
        await harness.RunAsync();

        // Assert
        Assert.Equal("https://example.test/hold.mp3", harness.Provider.HoldMusic.Single());
        Assert.Empty(harness.Provider.Spoken);
    }

    [Fact]
    public async Task AQueueThatAsksForNothingAtAll_IsStillSkipped()
    {
        // Arrange
        // The skip itself is worth keeping: this runs constantly against every queue, and a queue with no
        // treatment must not cost a read of everybody waiting in it.
        var harness = new TreatmentHarness();
        harness.Settings.WelcomeMessage = null;
        harness.Settings.HoldMusicMediaId = null;
        harness.Settings.AnnouncementIntervalSeconds = 0;
        harness.Settings.CallbackDtmfKey = null;
        harness.WithWaitingCaller("item-1", waitedSeconds: 1);

        // Act
        await harness.RunAsync();

        // Assert
        Assert.Empty(harness.Provider.HoldMusic);
        Assert.Empty(harness.Provider.Spoken);
    }

    [Fact]
    public async Task TheWelcome_IsSaidOnce()
    {
        // Arrange
        // The sweep runs every few seconds. Greeting somebody on every pass is the most audible way that shows up.
        var harness = new TreatmentHarness();
        harness.WithWaitingCaller("item-1", waitedSeconds: 1);

        // Act
        await harness.RunAsync();
        await harness.RunAsync();

        // Assert
        Assert.Single(harness.Provider.Spoken);
    }

    [Fact]
    public async Task AWaitingCaller_IsToldTheirPlaceInLine()
    {
        // Arrange
        var harness = new TreatmentHarness();
        harness.Settings.WelcomeMessage = null;
        harness.Settings.AnnouncePosition = true;
        harness.WithWaitingCaller("item-1", waitedSeconds: 120, stepsPlayed: 1);
        harness.WithWaitingCaller("item-2", waitedSeconds: 90, stepsPlayed: 1);

        // Act
        await harness.RunAsync();

        // Assert
        Assert.Contains("number 2 in line", harness.Provider.Spoken[1], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnEstimate_IsOnlyGivenWhenThereIsAnHonestOneToGive()
    {
        // Arrange
        // A queue with nobody working it is not moving, so quoting a wait would be a number invented to fill a
        // sentence. The caller is better told their position and nothing else.
        var harness = new TreatmentHarness();
        harness.Settings.WelcomeMessage = null;
        harness.Settings.AnnouncePosition = true;
        harness.Settings.AnnounceEstimatedWait = true;
        harness.AvailableAgents = 0;
        harness.WithWaitingCaller("item-1", waitedSeconds: 120, stepsPlayed: 1);

        // Act
        await harness.RunAsync();

        // Assert
        Assert.DoesNotContain("estimated", harness.Provider.Spoken.Single(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WithAgentsWorkingTheQueue_TheEstimateIsSpoken()
    {
        // Arrange
        var harness = new TreatmentHarness();
        harness.Settings.WelcomeMessage = null;
        harness.Settings.AnnouncePosition = true;
        harness.Settings.AnnounceEstimatedWait = true;
        harness.Settings.AverageHandleTimeSeconds = 300;
        harness.AvailableAgents = 2;
        harness.WithWaitingCaller("item-1", waitedSeconds: 120, stepsPlayed: 1);

        // Act
        await harness.RunAsync();

        // Assert
        Assert.Contains("estimated", harness.Provider.Spoken.Single(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnAnnouncementWithNothingToSay_DoesNotInterruptTheMusic()
    {
        // Arrange
        // A queue that announces neither position nor wait has configured a cadence with no content. Speaking an
        // empty sentence stops the hold music for nothing.
        var harness = new TreatmentHarness();
        harness.Settings.WelcomeMessage = null;
        harness.Settings.AnnouncePosition = false;
        harness.Settings.AnnounceEstimatedWait = false;
        harness.WithWaitingCaller("item-1", waitedSeconds: 120, stepsPlayed: 1);

        // Act
        await harness.RunAsync();

        // Assert
        Assert.Empty(harness.Provider.Spoken);
    }

    [Fact]
    public async Task TheCallbackOffer_IsMadeOnceAndRecorded()
    {
        // Arrange
        // Offering repeatedly would re-prompt a caller who already declined, every few seconds, for the rest of
        // their wait.
        var harness = new TreatmentHarness();
        harness.Settings.WelcomeMessage = null;
        harness.Settings.CallbackDtmfKey = "1";
        harness.Settings.CallbackOfferAfterSeconds = 60;
        var item = harness.WithWaitingCaller("item-1", waitedSeconds: 120, stepsPlayed: 1);

        // Act
        await harness.RunAsync();
        await harness.RunAsync();

        // Assert
        Assert.Single(harness.Provider.Offers);
        Assert.Equal(_now, item.CallbackOfferedUtc);
    }

    [Fact]
    public async Task ACallerWithNoLiveLeg_IsSkippedRatherThanRecordedAsTreated()
    {
        // Arrange
        // A queued item whose call has not been answered yet, or has already gone, has nothing to speak on.
        // Marking it treated would silently consume its welcome.
        var harness = new TreatmentHarness();
        var item = harness.WithWaitingCaller("item-1", waitedSeconds: 1, providerCallId: null);

        // Act
        await harness.RunAsync();

        // Assert
        Assert.Empty(harness.Provider.Spoken);
        Assert.Equal(0, item.TreatmentStepsPlayed);
        Assert.Null(item.LastTreatmentUtc);
    }

    [Fact]
    public async Task AQueueWithNoTreatmentConfigured_ReadsNobodyOutOfTheDatabase()
    {
        // Arrange
        // The sweep runs every few seconds against every queue. A queue that configures no treatment must cost
        // nothing at all.
        var harness = new TreatmentHarness();
        harness.Queue.Treatment = new QueueTreatmentSettings();

        // Act
        await harness.RunAsync();

        // Assert
        harness.AssertNoWaitingCallersWereRead();
    }

    [Fact]
    public async Task OneCallerWhoHungUp_DoesNotStopTheRest()
    {
        // Arrange
        // Treatment is a sweep over everybody waiting. A leg that has just gone must not take the pass down with
        // it and leave every other caller in silence.
        var harness = new TreatmentHarness();
        harness.WithWaitingCaller("item-1", waitedSeconds: 1);
        harness.WithWaitingCaller("item-2", waitedSeconds: 1);
        harness.Provider.ThrowOnCall("call-item-1");

        // Act
        var treated = await harness.RunAsync();

        // Assert
        Assert.Equal(1, treated);
    }

    private sealed class TreatmentHarness
    {
        private readonly List<QueueItem> _waiting = [];
        private readonly Dictionary<string, string> _providerCallIds = new(StringComparer.Ordinal);
        private readonly Mock<IQueueItemManager> _queueItemManager = new();

        public TreatmentHarness()
        {
            Queue = new ActivityQueue
            {
                ItemId = "queue-1",
                Treatment = Settings,
            };

            _queueItemManager.Setup(x => x.GetWaitingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _waiting);

            var interactionManager = new Mock<IInteractionManager>();
            interactionManager.Setup(x => x.FindByActivityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string activityId, CancellationToken _) =>
                    _providerCallIds.TryGetValue(activityId, out var callId) && callId is not null
                        ? new Interaction { ItemId = $"interaction-{activityId}", ProviderInteractionId = callId }
                        : null);

            var availability = new Mock<IAgentAvailabilityService>();
            availability.Setup(x => x.GetForQueueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Enumerable.Range(0, AvailableAgents)
                    .Select(i => new AgentAvailability { Agent = new AgentProfile { ItemId = $"agent-{i}" } })
                    .ToArray());

            var clock = new Mock<IClock>();
            clock.SetupGet(x => x.UtcNow).Returns(_now);

            Service = new QueueTreatmentService(
                _queueItemManager.Object,
                interactionManager.Object,
                Provider,
                availability.Object,
                clock.Object,
                NullLogger<QueueTreatmentService>.Instance);
        }

        public QueueTreatmentSettings Settings { get; } = new()
        {
            WelcomeMessage = "Thanks for calling. We will be with you shortly.",
            HoldMusicMediaId = "https://example.test/hold.mp3",
            AnnouncementIntervalSeconds = 30,
        };

        public ActivityQueue Queue { get; }

        public RecordingTreatmentProvider Provider { get; } = new();

        public QueueTreatmentService Service { get; }

        public int AvailableAgents { get; set; } = 1;

        public QueueItem WithWaitingCaller(string itemId, int waitedSeconds, int stepsPlayed = 0, string providerCallId = "default")
        {
            var item = new QueueItem
            {
                ItemId = itemId,
                QueueId = "queue-1",
                ActivityItemId = $"activity-{itemId}",
                QueueEnteredUtc = _now.AddSeconds(-waitedSeconds),
                TreatmentStepsPlayed = stepsPlayed,
                LastTreatmentUtc = stepsPlayed > 0 ? _now.AddSeconds(-waitedSeconds) : null,
            }.RestorePersistedStatus(QueueItemStatus.Waiting);

            _waiting.Add(item);
            _providerCallIds[item.ActivityItemId] = providerCallId == "default" ? $"call-{itemId}" : providerCallId;

            return item;
        }

        public Task<int> RunAsync()
            => Service.RunDueAsync(Queue, TestContext.Current.CancellationToken);

        public void AssertNoWaitingCallersWereRead()
            => _queueItemManager.Verify(
                x => x.GetWaitingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
    }

    /// <summary>
    /// A provider that records what a caller would have heard.
    /// </summary>
    private sealed class RecordingTreatmentProvider : IQueueTreatmentProvider
    {
        private string _failingCallId;

        public List<string> Spoken { get; } = [];

        public List<string> HoldMusic { get; } = [];

        public List<string> Offers { get; } = [];

        public void ThrowOnCall(string providerCallId)
            => _failingCallId = providerCallId;

        public Task SpeakAsync(string providerCallId, string text, CancellationToken cancellationToken = default)
        {
            Fail(providerCallId);
            Spoken.Add(text);

            return Task.CompletedTask;
        }

        public Task StartHoldMusicAsync(string providerCallId, string mediaId, CancellationToken cancellationToken = default)
        {
            Fail(providerCallId);

            if (!string.IsNullOrEmpty(mediaId))
            {
                HoldMusic.Add(mediaId);
            }

            return Task.CompletedTask;
        }

        public Task OfferChoiceAsync(string providerCallId, string text, string acceptKey, CancellationToken cancellationToken = default)
        {
            Fail(providerCallId);
            Offers.Add(text);

            return Task.CompletedTask;
        }

        private void Fail(string providerCallId)
        {
            if (string.Equals(providerCallId, _failingCallId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The call has already ended.");
            }
        }
    }
}
