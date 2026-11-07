using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.AI;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.AI;

/// <summary>
/// Explains one activity. The case that cost a manager an afternoon: a hand-created activity with an empty channel and no
/// number of its own, whose contact does have a number.
/// </summary>
public sealed class ExplainActivityToolTests
{
    private const string CampaignId = "leadgen";
    private const string ProfileId = "profile-preview";

    private static readonly string _campaignQueueId = ContactCenterConstants.CampaignQueue.CreateId(CampaignId);

    [Fact]
    public async Task InvokeAsync_WhenTheChannelIsEmptyButTheContactHasANumber_SaysItIsDialableAndChangesNothing()
    {
        // Arrange
        var activity = NewActivity();
        var host = HostFor(activity, eligibility: context =>
        {
            // What the dialer's own gate does: it takes the contact's current number when the record has none.
            context.Activity.PreferredDestination = "+17025550123";

            return DialerEligibilityResult.Eligible();
        });

        // Act
        var result = await host.RunAsync(new ExplainActivityTool(), ("activityId", activity.ItemId));

        // Assert
        Assert.Contains("DestinationFromContact", ContactCenterToolHost.FindingCodes(result));
        Assert.Contains("can be dialed or offered now", result.GetProperty("verdict").GetString(), StringComparison.Ordinal);
        Assert.Equal("(empty)", result.GetProperty("activity").GetProperty("channel").GetString());
        Assert.False(result.GetProperty("activity").GetProperty("hasOwnNumber").GetBoolean());
        Assert.Null(activity.PreferredDestination);
        Assert.Contains("0123", result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("7025550123", result.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheChannelIsEmptyAndTheContactHasNoNumber_SaysTheDialerFailsItForGood()
    {
        // Arrange
        var activity = NewActivity();
        var host = HostFor(activity, eligibility: _ => DialerEligibilityResult.Suppressed(DialerSuppressionReason.NoDestination, "The activity has no destination to dial."));

        // Act
        var result = await host.RunAsync(new ExplainActivityTool(), ("activityId", activity.ItemId));

        // Assert
        var finding = result.GetProperty("findings")[0];
        Assert.Equal("Suppressed.NoDestination", finding.GetProperty("code").GetString());
        Assert.Equal("certain", finding.GetProperty("likelihood").GetString());
        Assert.Contains("channel is empty and its contact has no phone number", finding.GetProperty("summary").GetString(), StringComparison.Ordinal);
        Assert.Contains("fails such a record for good", finding.GetProperty("summary").GetString(), StringComparison.Ordinal);
        Assert.Contains("is not being dialed", result.GetProperty("verdict").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheActivityIsAlreadyFailed_SaysItIsClosedAndNeverRetried()
    {
        // Arrange
        var activity = NewActivity();
        activity.Status = ActivityStatus.Failed;
        activity.TerminalReasonCode = "no-destination";
        var host = HostFor(activity);

        // Act
        var result = await host.RunAsync(new ExplainActivityTool(), ("activityId", activity.ItemId));

        // Assert
        Assert.Equal("NotPending", result.GetProperty("findings")[0].GetProperty("code").GetString());
        Assert.Contains("no-destination", result.GetProperty("findings")[0].GetProperty("summary").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_WhenThePendingActivityIsAssignedToAUserAndInNoQueue_SaysItIsOnTheirOwnList()
    {
        // Arrange
        var activity = NewActivity();
        activity.AssignedToId = "user-5";
        activity.AssignedToUsername = "maria";
        var host = HostFor(activity, queueItem: null, blocker: DispatchBlocker.AssignedToUser);

        // Act
        var result = await host.RunAsync(new ExplainActivityTool(), ("activityId", activity.ItemId));

        // Assert
        Assert.Equal("AssignedToUser", result.GetProperty("findings")[0].GetProperty("code").GetString());
        Assert.Contains("maria", result.GetProperty("findings")[0].GetProperty("summary").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheActivityIsNotFound_SaysSo()
    {
        // Arrange
        var host = HostFor(NewActivity());

        // Act
        var result = await host.RunAsync(new ExplainActivityTool(), ("activityId", "missing"));

        // Assert
        Assert.Contains("No activity has the id", result.GetProperty("error").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheCampaignIsNotOverseen_RefusesToExplain()
    {
        // Arrange
        var activity = NewActivity();
        var host = HostFor(activity);
        host.DoesNotOversee(_campaignQueueId);

        // Act
        var result = await host.RunAsync(new ExplainActivityTool(), ("activityId", activity.ItemId));

        // Assert
        Assert.Contains("don't oversee", result.GetProperty("error").GetString(), StringComparison.Ordinal);
    }

    private static OmnichannelActivity NewActivity()
        => new()
        {
            ItemId = "act-e",
            Status = ActivityStatus.Pending,
            Channel = string.Empty,
            PreferredDestination = null,
            CampaignId = CampaignId,
            DialerProfileId = ProfileId,
            InteractionType = ActivityInteractionType.Manual,
            ScheduledUtc = ContactCenterToolHost.Now.AddDays(-1),
            CreatedUtc = ContactCenterToolHost.Now.AddDays(-1),
        };

    private static ContactCenterToolHost HostFor(
        OmnichannelActivity activity,
        Func<DialerEligibilityContext, DialerEligibilityResult> eligibility = null,
        QueueItem queueItem = null,
        DispatchBlocker blocker = DispatchBlocker.DialableNow)
    {
        queueItem ??= new QueueItem
        {
            ItemId = "qi-1",
            QueueId = _campaignQueueId,
            ActivityItemId = activity.ItemId,
            DialerProfileId = ProfileId,
            EnqueuedUtc = ContactCenterToolHost.Now.AddHours(-1),
        }.RestorePersistedStatus(QueueItemStatus.Waiting);

        if (blocker == DispatchBlocker.AssignedToUser)
        {
            queueItem = null;
        }

        var host = new ContactCenterToolHost()
            .WithQueues()
            .WithCampaigns((CampaignId, "Lead Generation"))
            .WithAgents()
            .WithDecisionLog();
        var profile = new DialerProfile { ItemId = ProfileId, Name = "Preview dialing", Mode = DialerMode.Preview, Enabled = true };

        var activities = new Mock<IOmnichannelActivityManager>();
        activities
            .Setup(manager => manager.FindByIdAsync(activity.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);
        activities
            .Setup(manager => manager.FindByIdAsync(It.Is<string>(id => id != activity.ItemId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OmnichannelActivity)null);

        var items = new Mock<IQueueItemManager>();
        items
            .Setup(manager => manager.FindByActivityIdAsync(activity.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueItem);

        var reservations = new Mock<IActivityReservationManager>();
        reservations
            .Setup(manager => manager.GetActiveByActivityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var interactions = new Mock<IInteractionManager>();
        interactions
            .Setup(manager => manager.FindByActivityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Interaction)null);

        host.Services.AddSingleton(activities.Object);
        host.Services.AddSingleton(items.Object);
        host.Services.AddSingleton(reservations.Object);
        host.Services.AddSingleton(interactions.Object);
        host.Services.AddSingleton<DialerRecordClassifier>(new FakeClassifier(profile, blocker));

        if (eligibility is not null)
        {
            var service = new Mock<IDialerEligibilityService>();
            service
                .Setup(value => value.EvaluateAsync(It.IsAny<DialerEligibilityContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DialerEligibilityContext context, CancellationToken _) => eligibility(context));
            host.Services.AddSingleton(service.Object);
        }

        return host;
    }

    private sealed class FakeClassifier : DialerRecordClassifier
    {
        private readonly DialerProfile _profile;
        private readonly DispatchBlocker _blocker;

        public FakeClassifier(DialerProfile profile, DispatchBlocker blocker)
            : base(null, null, null, null, null)
        {
            _profile = profile;
            _blocker = blocker;
        }

        public override Task<DialerProfile> ReadProfileAsync(string profileId, CancellationToken cancellationToken)
            => Task.FromResult(_profile);

        public override Task<IReadOnlyDictionary<string, DialerRecordVerdict>> ClassifyAsync(
            IReadOnlyCollection<DialerRecordInput> records,
            DateTime now,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<string, DialerRecordVerdict>>(
                records.ToDictionary(record => record.ActivityId, _ => new DialerRecordVerdict(_blocker, null, null), StringComparer.Ordinal));
    }
}
