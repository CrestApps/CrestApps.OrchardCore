using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using CrestApps.OrchardCore.ContactCenter.Workflows.Services;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Production, 2026-11-10: every Contact Center event, agent heartbeats included, wrote a deduplication row and
/// queried the workflow engine even though the only workflow listened for dispositions. An event no enabled workflow
/// listens for now stops before anything is read or written.
/// </summary>
public sealed class ContactCenterWorkflowSubscriptionsTests
{
    private static readonly DateTime _now = new(2026, 11, 10, 23, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AnEventNoWorkflowListensFor_WritesNothingAndTriggersNothing()
    {
        // Arrange
        var workflowManager = new Mock<IWorkflowManager>();
        var deduplication = new Mock<IContactCenterEventDeduplicationService>();
        var subscriptions = Subscriptions(Workflow(true, Event("ActivityDispositionApplied", isStart: true)));
        var handler = new ContactCenterWorkflowEventHandler(workflowManager.Object, deduplication.Object, subscriptions);

        // Act
        await handler.HandleAsync(new InteractionEvent { ItemId = "event-1", EventType = "AgentConnected" }, TestContext.Current.CancellationToken);

        // Assert
        deduplication.Verify(service => service.TryBeginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        workflowManager.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AnEventAWorkflowListensFor_IsHandedToTheWorkflowEngine()
    {
        // Arrange
        var workflowManager = new Mock<IWorkflowManager>();
        var deduplication = new Mock<IContactCenterEventDeduplicationService>();
        deduplication
            .Setup(service => service.TryBeginAsync(It.IsAny<string>(), "event-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var subscriptions = Subscriptions(Workflow(true, Event("ActivityDispositionApplied", isStart: true)));
        var handler = new ContactCenterWorkflowEventHandler(workflowManager.Object, deduplication.Object, subscriptions);

        // Act
        await handler.HandleAsync(new InteractionEvent { ItemId = "event-1", EventType = "ActivityDispositionApplied", AggregateId = "activity-1" }, TestContext.Current.CancellationToken);

        // Assert
        workflowManager.Verify(
            manager => manager.TriggerEventAsync(nameof(ContactCenterEvent), It.IsAny<IDictionary<string, object>>(), "activity-1", It.IsAny<bool>(), It.IsAny<bool>()),
            Times.Once);
    }

    [Fact]
    public void TheSnapshot_ListensForEveryEventType_OfEnabledWorkflowsOnly()
    {
        // Arrange
        var snapshot = ContactCenterWorkflowSubscriptionSnapshot.From(
        [
            Workflow(true, Event("ActivityDispositionApplied", isStart: true)),
            Workflow(true, Event("DialerAttemptCompleted", isStart: false)),
            Workflow(false, Event("OfferAccepted", isStart: true)),
        ]);

        // Assert
        Assert.True(snapshot.Takes("ActivityDispositionApplied"));
        Assert.True(snapshot.Takes("activitydispositionapplied"));
        Assert.True(snapshot.Takes("DialerAttemptCompleted"));
        Assert.False(snapshot.Takes("OfferAccepted"));
        Assert.False(snapshot.Takes("AgentConnected"));
        Assert.False(snapshot.Takes(null));
    }

    [Fact]
    public void AContactCenterEventWithNoEventType_ListensForEveryEvent()
    {
        // Arrange
        var snapshot = ContactCenterWorkflowSubscriptionSnapshot.From([Workflow(true, Event(null, isStart: true))]);

        // Assert
        Assert.True(snapshot.Takes("AgentConnected"));
    }

    [Fact]
    public async Task TheWorkflowTypes_AreReadOnce_UntilAWorkflowTypeChangesOrTheSnapshotExpires()
    {
        // Arrange
        var store = new Mock<IWorkflowTypeStore>();
        store.Setup(s => s.ListAsync()).ReturnsAsync([Workflow(true, Event("ActivityDispositionApplied", isStart: true))]);
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(_now);
        var cache = new ContactCenterWorkflowSubscriptionCache();
        var subscriptions = new ContactCenterWorkflowSubscriptions(store.Object, cache, clock.Object);

        // Act
        await subscriptions.IsSubscribedAsync("AgentConnected");
        await subscriptions.IsSubscribedAsync("AgentHeartbeat");
        await subscriptions.IsSubscribedAsync("ActivityDispositionApplied");
        var readsBeforeChange = store.Invocations.Count;

        await new ContactCenterWorkflowSubscriptionInvalidator(cache).UpdatedAsync(new WorkflowTypeUpdatedContext(new WorkflowType()));
        await subscriptions.IsSubscribedAsync("AgentConnected");
        var readsAfterChange = store.Invocations.Count;

        clock.Setup(c => c.UtcNow).Returns(_now + ContactCenterWorkflowSubscriptionCache.Lifetime);
        await subscriptions.IsSubscribedAsync("AgentConnected");

        // Assert
        Assert.Equal(1, readsBeforeChange);
        Assert.Equal(2, readsAfterChange);
        Assert.Equal(3, store.Invocations.Count);
    }

    private static ContactCenterWorkflowSubscriptions Subscriptions(params WorkflowType[] workflowTypes)
    {
        var store = new Mock<IWorkflowTypeStore>();
        store.Setup(s => s.ListAsync()).ReturnsAsync(workflowTypes);
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(_now);

        return new ContactCenterWorkflowSubscriptions(store.Object, new ContactCenterWorkflowSubscriptionCache(), clock.Object);
    }

    private static WorkflowType Workflow(bool enabled, ActivityRecord activity)
        => new() { IsEnabled = enabled, Activities = [activity] };

    private static ActivityRecord Event(string eventType, bool isStart)
    {
        var activity = new ActivityRecord { Name = nameof(ContactCenterEvent), IsStart = isStart };

        if (eventType is not null)
        {
            activity.Properties[nameof(ContactCenterEvent.EventType)] = JsonValue.Create(eventType);
        }

        return activity;
    }
}
