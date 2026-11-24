using CrestApps.OrchardCore.ContactCenter.AI;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Tests.Agent;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.AITools;

/// <summary>
/// <c>listCallbackRequests</c> shows an agent only their own callbacks, checks its filters before it reads anything, and
/// explains where a callback is from the callback and the call activity it was promoted to.
/// </summary>
public sealed class ContactCenterCallbackToolTests
{
    private static readonly DateTime _now = ContactCenterSetupToolHarness.Now;

    private readonly ContactCenterSetupToolHarness _harness = new();

    [Fact]
    public async Task ListCallbacks_TellsAnAgentWithoutAProfileThatNoneIsTheirs()
    {
        // Arrange: an agent who may sign in to queues but not see everyone's callbacks.
        var services = _harness.Build(authorize: (permission, _) => permission.Name == ContactCenterPermissions.SignIntoQueues.Name);

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListCallbackRequestsTool(), services));

        // Assert
        Assert.True(json.GetProperty("onlyOwnCallbacks").GetBoolean());
        Assert.Equal(0, json.GetProperty("total").GetInt32());
        Assert.Contains("no agent profile", json.GetProperty("note").GetString());
    }

    [Fact]
    public async Task ListCallbacks_RefusesACallerWhoIsNeitherAgentNorSupervisor()
    {
        // Arrange
        var services = _harness.Build(authorize: (permission, _) => permission.Name == ContactCenterPermissions.ManageQueues.Name);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListCallbackRequestsTool(), services);

        // Assert
        Assert.Equal(ContactCenterSetupToolBase.NotAllowedMessage, result);
    }

    [Fact]
    public async Task ListCallbacks_AsksForEnoughDigitsOfTheNumber()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListCallbackRequestsTool(), _harness.Build(), new() { ["phoneNumber"] = "12" });

        // Assert
        Assert.Equal("phoneNumber must hold at least 4 digits of the number called back.", result);
    }

    [Fact]
    public async Task ListCallbacks_SaysSoWhenTheAgentFilterNamesNobody()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListCallbackRequestsTool(), _harness.Build(), new() { ["agentId"] = "nobody" });

        // Assert
        Assert.Equal("No agent has the ID or user name 'nobody'.", result);
    }

    [Fact]
    public async Task ListCallbacks_RefusesAStateThatDoesNotExist()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListCallbackRequestsTool(), _harness.Build(), new() { ["status"] = "Lost" });

        // Assert
        Assert.Contains("must be one of: Pending, Scheduled, InProgress, Completed, Canceled, Failed.", result);
    }

    [Fact]
    public void Explain_SaysADueCallbackThatStaysPendingWasNotPromoted()
    {
        // Act
        var explanation = ListCallbackRequestsTool.Explain(new CallbackRequest { Status = CallbackRequestStatus.Pending, ScheduledUtc = _now.AddMinutes(-30) }, null, _now);

        // Assert
        Assert.Contains("not promoted yet", explanation);
        Assert.Contains("Callback Dispatch", explanation);
    }

    [Fact]
    public void Explain_SaysAPendingCallbackWaitsForItsTime()
    {
        // Act
        var explanation = ListCallbackRequestsTool.Explain(new CallbackRequest { Status = CallbackRequestStatus.Pending, ScheduledUtc = _now.AddHours(2) }, null, _now);

        // Assert
        Assert.StartsWith("Waiting for its time", explanation);
    }

    [Fact]
    public void Explain_SaysAPromotedCallbackOutsideAnyQueueNeedsAnAssignee()
    {
        // Arrange
        var callback = new CallbackRequest { Status = CallbackRequestStatus.Scheduled, ScheduledUtc = _now.AddHours(-1), ActivityItemId = "activity-1" };
        var activity = new OmnichannelActivity { ItemId = "activity-1", Status = ActivityStatus.NotStated };

        // Act
        var explanation = ListCallbackRequestsTool.Explain(callback, activity, _now);

        // Assert
        Assert.Contains("not in a queue and not assigned", explanation);
    }

    [Fact]
    public void Explain_PointsToTheDispositionOfACompletedActivity()
    {
        // Arrange
        var callback = new CallbackRequest { Status = CallbackRequestStatus.Scheduled, QueueId = "sales", ActivityItemId = "activity-1" };
        var activity = new OmnichannelActivity { ItemId = "activity-1", Status = ActivityStatus.Completed };

        // Act
        var explanation = ListCallbackRequestsTool.Explain(callback, activity, _now);

        // Assert
        Assert.Contains("disposition", explanation);
    }

    [Theory]
    [InlineData(ActivityStatus.Cancelled, "canceled, so the callback was not made")]
    [InlineData(ActivityStatus.Purged, "purged, so it will never be worked")]
    [InlineData(ActivityStatus.Failed, "failed")]
    [InlineData(ActivityStatus.Dialing, "An agent has the call activity now")]
    public void Explain_TellsWhatHappenedToThePromotedCallsActivity(ActivityStatus status, string expected)
    {
        // Arrange: a promoted callback stays Scheduled; its call activity carries the outcome.
        var callback = new CallbackRequest { Status = CallbackRequestStatus.Scheduled, QueueId = "sales", ActivityItemId = "activity-1" };
        var activity = new OmnichannelActivity { ItemId = "activity-1", Status = status };

        // Act
        var explanation = ListCallbackRequestsTool.Explain(callback, activity, _now);

        // Assert
        Assert.Contains(expected, explanation);
        Assert.DoesNotContain("waiting for an agent", explanation);
    }

    [Fact]
    public void Explain_SaysAPromotedCallbackWhoseActivityIsGoneCannotBeWorked()
    {
        // Act
        var explanation = ListCallbackRequestsTool.Explain(new CallbackRequest { Status = CallbackRequestStatus.Scheduled, ActivityItemId = "activity-9" }, null, _now);

        // Assert
        Assert.Contains("can no longer be found", explanation);
    }
}
