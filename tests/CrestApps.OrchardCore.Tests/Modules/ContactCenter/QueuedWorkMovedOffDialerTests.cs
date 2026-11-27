using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Routing drops a record from a campaign's dialer queue once its activity is no longer dialer work, so the dialer does
/// not call an activity a workflow or a bulk action gave to an agent.
/// </summary>
public sealed class QueuedWorkMovedOffDialerTests
{
    [Fact]
    public void IsMovedOffDialer_WhenADialerRecordsActivityBecameManual_IsTrue()
    {
        var queueItem = CampaignItem("profile-1");
        var activity = new OmnichannelActivity { Source = ActivitySources.Manual };

        Assert.True(QueuedWorkWithdrawalService.IsMovedOffDialer(queueItem, activity));
    }

    [Theory]
    [InlineData(ActivitySources.PowerDial)]
    [InlineData(ActivitySources.PredictiveDial)]
    [InlineData(ActivitySources.PreviewDial)]
    public void IsMovedOffDialer_WhenTheActivityIsStillDialerWork_IsFalse(string source)
    {
        var queueItem = CampaignItem("profile-1");
        var activity = new OmnichannelActivity { Source = source };

        Assert.False(QueuedWorkWithdrawalService.IsMovedOffDialer(queueItem, activity));
    }

    [Fact]
    public void IsMovedOffDialer_WhenTheRecordWaitsInARealQueue_IsFalse()
    {
        // Manual work an Enqueue Activity task put in a queue is routed there for its own reasons.
        var queueItem = new QueueItem { QueueId = "sales", DialerProfileId = null };
        var activity = new OmnichannelActivity { Source = ActivitySources.Manual };

        Assert.False(QueuedWorkWithdrawalService.IsMovedOffDialer(queueItem, activity));
    }

    [Fact]
    public void IsMovedOffDialer_WhenTheRecordIsACallback_IsFalse()
    {
        var queueItem = CampaignItem(QueueCallbackDialerProfile.Id);
        var activity = new OmnichannelActivity { Source = ActivitySources.Callback };

        Assert.False(QueuedWorkWithdrawalService.IsMovedOffDialer(queueItem, activity));
    }

    private static QueueItem CampaignItem(string dialerProfileId)
        => new()
        {
            QueueId = ContactCenterConstants.CampaignQueue.CreateId("campaign-1"),
            DialerProfileId = dialerProfileId,
        };
}
