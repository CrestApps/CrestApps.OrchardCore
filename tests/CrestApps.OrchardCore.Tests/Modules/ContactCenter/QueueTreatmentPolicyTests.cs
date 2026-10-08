using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// A caller on hold with no announcements has no idea whether the queue is working, so they abandon. The policy
/// decides what the caller hears and when: a welcome once, then a periodic update, and the offer of a callback
/// they can take instead of waiting.
/// </summary>
public sealed class QueueTreatmentPolicyTests
{
    private static readonly DateTime _now = new(2026, 3, 4, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FirstStep_IsTheWelcome()
    {
        // Arrange
        var settings = Options(welcome: "Thanks for calling.");
        var item = Item(_now);

        // Act
        var step = QueueTreatmentPolicy.GetNextStep(item, settings, _now);

        // Assert
        Assert.Equal(QueueTreatmentStepKind.Welcome, step.Kind);
        Assert.Equal("Thanks for calling.", step.Text);
    }

    [Fact]
    public void Welcome_IsPlayedOnce()
    {
        // Arrange
        // A welcome repeated every thirty seconds tells the caller the system has forgotten them.
        var settings = Options(welcome: "Thanks for calling.");
        var item = Item(_now);
        item.TreatmentStepsPlayed = 1;

        // Act
        var step = QueueTreatmentPolicy.GetNextStep(item, settings, _now.AddSeconds(1));

        // Assert
        Assert.NotEqual(QueueTreatmentStepKind.Welcome, step.Kind);
    }

    [Fact]
    public void Announcement_WaitsForTheCadence()
    {
        // Arrange
        var settings = Options(welcome: null, announcementSeconds: 30);
        var item = Item(_now);
        item.LastTreatmentUtc = _now;

        // Act
        var step = QueueTreatmentPolicy.GetNextStep(item, settings, _now.AddSeconds(10));

        // Assert
        Assert.Equal(QueueTreatmentStepKind.None, step.Kind);
    }

    [Fact]
    public void Announcement_FiresOnceTheCadenceHasElapsed()
    {
        // Arrange
        var settings = Options(welcome: null, announcementSeconds: 30);
        var item = Item(_now);
        item.LastTreatmentUtc = _now;

        // Act
        var step = QueueTreatmentPolicy.GetNextStep(item, settings, _now.AddSeconds(31));

        // Assert
        Assert.Equal(QueueTreatmentStepKind.Announcement, step.Kind);
    }

    [Fact]
    public void CallbackOffer_ComesBeforeTheFirstAnnouncement_WhenItIsEnabled()
    {
        // Arrange
        // The offer is only useful while the caller still has the patience to accept it; making them sit through
        // announcements first defeats the point of offering.
        var settings = Options(welcome: null, announcementSeconds: 30, callbackKey: "1", callbackAfterSeconds: 20);
        var item = Item(_now.AddSeconds(-25));
        item.LastTreatmentUtc = _now.AddSeconds(-25);

        // Act
        var step = QueueTreatmentPolicy.GetNextStep(item, settings, _now);

        // Assert
        Assert.Equal(QueueTreatmentStepKind.CallbackOffer, step.Kind);
        Assert.Equal("1", step.DtmfKey);
    }

    [Fact]
    public void CallbackOffer_IsMadeOnce()
    {
        // Arrange
        var settings = Options(welcome: null, announcementSeconds: 30, callbackKey: "1", callbackAfterSeconds: 20);
        var item = Item(_now.AddSeconds(-60));
        item.LastTreatmentUtc = _now.AddSeconds(-60);
        item.CallbackOfferedUtc = _now.AddSeconds(-30);

        // Act
        var step = QueueTreatmentPolicy.GetNextStep(item, settings, _now);

        // Assert
        Assert.Equal(QueueTreatmentStepKind.Announcement, step.Kind);
    }

    [Fact]
    public void NoTreatmentConfigured_MeansNothingIsPlayed()
    {
        // Arrange
        // Silence is the correct default: a queue that has configured nothing has not asked for its callers to
        // be talked at.
        var settings = new QueueTreatmentSettings();
        var item = Item(_now.AddMinutes(-10));

        // Act
        var step = QueueTreatmentPolicy.GetNextStep(item, settings, _now);

        // Assert
        Assert.Equal(QueueTreatmentStepKind.None, step.Kind);
    }

    [Fact]
    public void AQueueThatOnlyPlaysMusic_StartsIt()
    {
        // Arrange
        // Music used to start only as a side effect of the welcome, so a queue that plays music and says nothing
        // left the caller in silence for their entire wait — which sounds exactly like a dropped call. This is
        // the most ordinary hold configuration there is.
        var settings = Options(welcome: null, holdMusicMediaId: "media-1");
        var item = Item(_now);

        // Act
        var step = QueueTreatmentPolicy.GetNextStep(item, settings, _now);

        // Assert
        Assert.Equal(QueueTreatmentStepKind.HoldMusic, step.Kind);
    }

    [Fact]
    public void WhenThereIsAWelcome_TheMusicStartsBehindItRatherThanTwice()
    {
        // Arrange
        // The welcome already starts the music, so a separate music step would restart it a moment later.
        var settings = Options(welcome: "Thanks for calling.", holdMusicMediaId: "media-1");
        var item = Item(_now);

        // Act
        var step = QueueTreatmentPolicy.GetNextStep(item, settings, _now);

        // Assert
        Assert.Equal(QueueTreatmentStepKind.Welcome, step.Kind);
    }

    [Fact]
    public void TheMusicIsStartedOnce_NotOnEverySweep()
    {
        // Arrange
        // The sweep runs constantly; restarting the track every pass would keep the caller at the first bar.
        var settings = Options(welcome: null, holdMusicMediaId: "media-1");
        var item = Item(_now);
        item.TreatmentStepsPlayed = 1;

        // Act
        var step = QueueTreatmentPolicy.GetNextStep(item, settings, _now.AddSeconds(30));

        // Assert
        Assert.NotEqual(QueueTreatmentStepKind.HoldMusic, step.Kind);
    }

    private static QueueTreatmentSettings Options(
        string welcome,
        int announcementSeconds = 0,
        string callbackKey = null,
        int callbackAfterSeconds = 0,
        string holdMusicMediaId = null)
        => new()
        {
            WelcomeMessage = welcome,
            AnnouncementIntervalSeconds = announcementSeconds,
            CallbackDtmfKey = callbackKey,
            CallbackOfferAfterSeconds = callbackAfterSeconds,
            HoldMusicMediaId = holdMusicMediaId,
        };

    private static QueueItem Item(DateTime enqueuedUtc)
        => new() { ItemId = "i1", QueueId = "q1", EnqueuedUtc = enqueuedUtc, QueueEnteredUtc = enqueuedUtc };
}
