using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Production, 2026-11-10: the "text when nobody answers" workflow sent its texts after Power dials, but each one went
/// out as a system message that belonged to no agent, so agents reported that no text was sent. The workflow also saved
/// a finished instance for every Contact Center event, agent heartbeats included.
/// </summary>
public sealed class DialerNoAnswerFollowUpTests
{
    [Fact]
    public async Task ASystemDisposition_OfAnAutomatedDial_NamesTheAgentTheDialerHeldForTheCall()
    {
        // Arrange
        // A Power dial reached an answering machine: the system applied No Answer, so nobody completed the activity.
        var published = await PublishAsync(
            ActivityDispositionSource.System,
            completedById: null,
            interactionAgentId: "agent-1");

        // Assert
        Assert.Null(published.CompletedById);
        Assert.Equal("user-1", published.AgentUserId);
    }

    [Fact]
    public async Task AnAgentDisposition_WithoutACall_NamesTheAgentWhoAppliedIt()
    {
        // Act
        var published = await PublishAsync(
            ActivityDispositionSource.Agent,
            completedById: "user-2",
            interactionAgentId: null);

        // Assert
        Assert.Equal("user-2", published.AgentUserId);
    }

    [Fact]
    public async Task ASystemDisposition_OfACallNoAgentWasHeldFor_NamesNoAgent()
    {
        // Act
        var published = await PublishAsync(
            ActivityDispositionSource.System,
            completedById: null,
            interactionAgentId: null);

        // Assert
        Assert.Null(published.AgentUserId);
    }

    [Theory]
    [InlineData("AgentConnected", false)]
    [InlineData("ActivityDispositionApplied", true)]
    [InlineData("activitydispositionapplied", true)]
    public void TheContactCenterEvent_StartsAWorkflowOnlyForItsEventType(string eventType, bool starts)
    {
        // Arrange
        var activity = CreateEvent("ActivityDispositionApplied");
        var context = CreateContext(WorkflowStatus.Starting, eventType);

        // Act
        var canStart = activity.CanExecute(context, new ActivityContext { Activity = activity, ActivityRecord = new ActivityRecord { IsStart = true } });

        // Assert
        Assert.Equal(starts, canStart);
    }

    [Fact]
    public void TheContactCenterEvent_WithNoEventType_StartsForEveryEvent()
    {
        // Arrange
        var activity = CreateEvent(null);
        var context = CreateContext(WorkflowStatus.Starting, "AgentConnected");

        // Act
        var canStart = activity.CanExecute(context, new ActivityContext { Activity = activity, ActivityRecord = new ActivityRecord { IsStart = true } });

        // Assert
        Assert.True(canStart);
    }

    [Fact]
    public void AWorkflowWaitingOnTheContactCenterEvent_StillResumesForOtherEvents()
    {
        // Arrange
        var activity = CreateEvent("ActivityDispositionApplied");
        var context = CreateContext(WorkflowStatus.Resuming, "AgentConnected");
        var activityContext = new ActivityContext { Activity = activity, ActivityRecord = new ActivityRecord { IsStart = false } };

        // Act
        var canResume = activity.CanExecute(context, activityContext);
        var result = activity.Resume(context, activityContext);

        // Assert
        Assert.True(canResume);
        Assert.Contains("Ignored", result.Outcomes);
    }

    private static async Task<ActivityDispositionEventData> PublishAsync(
        ActivityDispositionSource source,
        string completedById,
        string interactionAgentId)
    {
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByActivityIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction { ItemId = "interaction-1", AgentId = interactionAgentId });

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByIdAsync("agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "user-1" });

        InteractionEvent published = null;
        var publisher = new Mock<IContactCenterEventPublisher>();
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) => published = interactionEvent)
            .Returns(Task.CompletedTask);

        var handler = new ActivityDispositionAppliedPublisher(
            Mock.Of<ICatalog<OmnichannelDisposition>>(),
            publisher.Object,
            interactionManager.Object,
            agentManager.Object,
            Mock.Of<ILogger<ActivityDispositionAppliedPublisher>>());

        await handler.DispositionedAsync(
            new ActivityDispositionRequest
            {
                Source = source,
                Activity = new OmnichannelActivity
                {
                    ItemId = "activity-1",
                    Status = ActivityStatus.Completed,
                    CompletedById = completedById,
                },
            },
            TestContext.Current.CancellationToken);

        Assert.NotNull(published);
        Assert.Equal(ContactCenterConstants.Events.ActivityDispositionApplied, published.EventType);

        return published.GetData<ActivityDispositionEventData>();
    }

    private static ContactCenterEvent CreateEvent(string eventType)
    {
        var localizer = new Mock<IStringLocalizer<ContactCenterEvent>>();
        localizer
            .Setup(l => l[It.IsAny<string>()])
            .Returns((string name) => new LocalizedString(name, name));

        return new ContactCenterEvent(localizer.Object) { EventType = eventType };
    }

    private static WorkflowExecutionContext CreateContext(WorkflowStatus status, string eventType)
    {
        var input = new Dictionary<string, object> { ["EventType"] = eventType };
        var context = new WorkflowExecutionContext(
            new WorkflowType { WorkflowTypeId = "t1" },
            new Workflow { WorkflowId = "w1" },
            input,
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            [],
            null,
            []);
        context.Status = status;

        return context;
    }
}
