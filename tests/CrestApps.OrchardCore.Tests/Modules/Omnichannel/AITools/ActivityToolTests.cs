using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.AI;
using CrestApps.OrchardCore.Tests.Agent;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.AITools;

/// <summary>
/// The activity tools apply the permission of the admin page they mirror, return only the listed fields of a customer
/// work item, and complete and purge through the same rules as the Complete page and Manage Activities.
/// </summary>
public sealed class ActivityToolTests
{
    private readonly OmnichannelToolHarness _harness = new();

    public ActivityToolTests()
    {
        _harness.AddContact();
    }

    private static Func<Permission, object, bool> Allow(params Permission[] permissions)
        => (permission, _) => permissions.Any(allowed => allowed.Name == permission.Name);

    [Fact]
    public async Task List_Mine_PagesTheCallersScheduledActivitiesWithoutContactDetails()
    {
        // Arrange
        var activity = _harness.AddActivity("activity-1", value => value.AssignedToId = AgentToolTestContext.UserId);
        string pagedFor = null;
        ListOmnichannelActivityFilter filter = null;
        _harness.ActivityManager
            .Setup(manager => manager.PageManualScheduledAsync(It.IsAny<string>(), 1, 20, It.IsAny<ListOmnichannelActivityFilter>(), It.IsAny<CancellationToken>()))
            .Callback((string userId, int _, int _, ListOmnichannelActivityFilter value, CancellationToken _) =>
            {
                pagedFor = userId;
                filter = value;
            })
            .ReturnsAsync(new PageResult<OmnichannelActivity> { Count = 1, Entries = [activity] });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListOmnichannelActivitiesTool(), _harness.Build(authorize: Allow(OmnichannelConstants.Permissions.ListActivities)), new()
        {
            ["scheduledFrom"] = "2026-11-18T00:00",
            ["urgencyLevel"] = "High",
        });

        // Assert
        var item = Assert.Single(AgentToolTestContext.ReadJson(result).GetProperty("items").EnumerateArray());
        Assert.Equal(AgentToolTestContext.UserId, pagedFor);
        Assert.Equal(new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc), filter.ScheduledFrom);
        Assert.Equal(ActivityUrgencyLevel.High, filter.UrgencyLevel);
        Assert.Equal("Jane Doe", item.GetProperty("contact").GetProperty("displayText").GetString());
        Assert.DoesNotContain("+15551234567", result);
        Assert.DoesNotContain("session-secret", result);
        Assert.DoesNotContain("Call before noon.", result);
    }

    [Fact]
    public async Task List_Contact_ChecksListContactActivitiesOnThatContact()
    {
        // Arrange
        var services = _harness.Build(authorize: (permission, resource) => permission.Name == OmnichannelConstants.Permissions.ListContactActivities.Name && resource is null);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListOmnichannelActivitiesTool(), services, new()
        {
            ["scope"] = "contact",
            ["contactContentItemId"] = "contact-1",
        });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        _harness.ActivityManager.Verify(manager => manager.PageContactManualScheduledAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task List_Contact_ReturnsTheCompletedActivitiesWhenAsked()
    {
        // Arrange
        var activity = _harness.AddActivity("activity-1", value => value.Status = ActivityStatus.Completed);
        _harness.ActivityManager
            .Setup(manager => manager.PageContactManualCompletedAsync("contact-1", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageResult<OmnichannelActivity> { Count = 1, Entries = [activity] });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListOmnichannelActivitiesTool(), _harness.Build(), new()
        {
            ["scope"] = "contact",
            ["contactContentItemId"] = "contact-1",
            ["completed"] = true,
        });

        // Assert
        Assert.Equal("Completed", Assert.Single(AgentToolTestContext.ReadJson(result).GetProperty("items").EnumerateArray()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task List_All_NeedsManageActivitiesAndPassesTheFilters()
    {
        // Arrange
        BulkManageActivityFilter filter = null;
        _harness.ActivityManager
            .Setup(manager => manager.PageBulkManageableAsync(1, 20, It.IsAny<BulkManageActivityFilter>(), It.IsAny<CancellationToken>()))
            .Callback((int _, int _, BulkManageActivityFilter value, CancellationToken _) => filter = value)
            .ReturnsAsync(new PageResult<OmnichannelActivity> { Count = 0, Entries = [] });
        var arguments = new Dictionary<string, object>
        {
            ["scope"] = "all",
            ["campaignId"] = "campaign-1",
            ["assignedToUserIds"] = new[] { "mark" },
            ["status"] = "NotStated",
        };

        // Act
        var refused = await AgentToolTestContext.InvokeAsync(new ListOmnichannelActivitiesTool(), _harness.Build(authorize: Allow(OmnichannelConstants.Permissions.ListActivities)), arguments);
        var listed = await AgentToolTestContext.InvokeAsync(new ListOmnichannelActivitiesTool(), _harness.Build(), arguments);

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, refused);
        Assert.Equal(0, AgentToolTestContext.ReadJson(listed).GetProperty("total").GetInt32());
        Assert.Equal("campaign-1", filter.CampaignId);
        Assert.Equal("mark", Assert.Single(filter.AssignedToUserIds));
        Assert.Equal(ActivityStatus.NotStated, filter.Status);
    }

    [Fact]
    public async Task List_RefusesAnUnknownScope()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListOmnichannelActivitiesTool(), _harness.Build(), new() { ["scope"] = "everything" });

        // Assert
        Assert.Equal("Nothing was listed: scope must be mine, contact or all.", result);
    }

    [Fact]
    public async Task Get_ReturnsTheCallersOwnActivityWithOnlyTheListedFields()
    {
        // Arrange
        _harness.AddActivity("activity-1", value =>
        {
            value.AssignedToId = AgentToolTestContext.UserId;
            value.Notes = "Asked for a call back.";
            value.HandoffSummary = "Wants a quote.";
        });
        var services = _harness.Build(authorize: (permission, resource) => permission.Name == OmnichannelConstants.Permissions.ListActivities.Name);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetOmnichannelActivityTool(), services, new() { ["id"] = "activity-1" });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        Assert.Equal("Asked for a call back.", json.GetProperty("notes").GetString());
        Assert.Equal("Wants a quote.", json.GetProperty("handoffSummary").GetString());
        Assert.True(json.GetProperty("ranByAISession").GetBoolean());
        Assert.DoesNotContain("+15551234567", result);
        Assert.DoesNotContain("session-secret", result);
        Assert.DoesNotContain("Call before noon.", result);
    }

    [Fact]
    public async Task Get_HidesAnotherUsersActivityFromACallerWhoCannotListTheContactsActivities()
    {
        // Arrange
        _harness.AddActivity("activity-1");
        var services = _harness.Build(authorize: (permission, resource) =>
            permission.Name == OmnichannelConstants.Permissions.ListActivities.Name ||
            (permission.Name == OmnichannelConstants.Permissions.ListContactActivities.Name && resource is not ContentItem));

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetOmnichannelActivityTool(), services, new() { ["id"] = "activity-1" });

        // Assert
        Assert.Equal("No activity you can see has the ID 'activity-1'. Call listOmnichannelActivities to find it.", result);
    }

    [Fact]
    public async Task Get_SaysSoWhenNoActivityHasTheId()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetOmnichannelActivityTool(), _harness.Build(), new() { ["id"] = "missing" });

        // Assert
        Assert.StartsWith("No activity you can see has the ID 'missing'", result);
    }

    [Fact]
    public async Task Complete_AppliesTheDispositionThroughTheDispositionService()
    {
        // Arrange
        var activity = _harness.AddActivity("activity-1");
        _harness.Dispositions.Add(new OmnichannelDisposition { ItemId = "d-1", Name = "Interested" });
        _harness.SubjectActions.Add(new SubjectAction { ItemId = "a-1", SubjectContentType = "SalesCall", DispositionId = "d-1" });
        ActivityDispositionRequest applied = null;
        _harness.DispositionService
            .Setup(service => service.ApplyAsync(It.IsAny<ActivityDispositionRequest>(), It.IsAny<CancellationToken>()))
            .Callback((ActivityDispositionRequest request, CancellationToken _) =>
            {
                applied = request;
                request.Activity.Status = ActivityStatus.Completed;
            })
            .ReturnsAsync((ActivityDispositionRequest request, CancellationToken _) => ActivityDispositionResult.Success(request.Activity));

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CompleteOmnichannelActivityTool(), _harness.Build(), new()
        {
            ["id"] = "activity-1",
            ["dispositionId"] = "d-1",
            ["notes"] = "Send the brochure.",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        Assert.Equal("Completed", json.GetProperty("completed").GetProperty("status").GetString());
        Assert.Equal("Interested", json.GetProperty("completed").GetProperty("disposition").GetProperty("name").GetString());
        Assert.Same(activity, applied.Activity);
        Assert.Equal("d-1", applied.DispositionId);
        Assert.Equal("Send the brochure.", applied.Notes);
        Assert.Equal(ActivityDispositionSource.Agent, applied.Source);
        Assert.Equal(AgentToolTestContext.UserId, applied.ActorId);
    }

    [Fact]
    public async Task Complete_RefusesADispositionTheSubjectDoesNotOffer()
    {
        // Arrange
        _harness.AddActivity("activity-1");
        _harness.Dispositions.Add(new OmnichannelDisposition { ItemId = "d-2", Name = "Other subject's" });
        _harness.SubjectActions.Add(new SubjectAction { ItemId = "a-1", SubjectContentType = "SupportCall", DispositionId = "d-2" });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CompleteOmnichannelActivityTool(), _harness.Build(), new()
        {
            ["id"] = "activity-1",
            ["dispositionId"] = "d-2",
        });

        // Assert
        Assert.Contains("is not one the subject 'SalesCall' offers", result);
        _harness.DispositionService.Verify(service => service.ApplyAsync(It.IsAny<ActivityDispositionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Complete_RefusesAnActivityThatIsAlreadyFinished()
    {
        // Arrange
        _harness.AddActivity("activity-1", value => value.Status = ActivityStatus.Purged);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CompleteOmnichannelActivityTool(), _harness.Build(), new() { ["id"] = "activity-1" });

        // Assert
        Assert.Contains("already completed", result);
        _harness.DispositionService.Verify(service => service.ApplyAsync(It.IsAny<ActivityDispositionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Complete_ChecksCompleteActivityOnTheActivity()
    {
        // Arrange: the caller may complete their own activities only, and this one is someone else's.
        _harness.AddActivity("activity-1");
        var services = _harness.Build(authorize: (permission, resource) =>
            permission.Name == OmnichannelConstants.Permissions.CompleteOwnActivity.Name ||
            (permission.Name == OmnichannelConstants.Permissions.CompleteActivity.Name && resource is null));

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CompleteOmnichannelActivityTool(), services, new() { ["id"] = "activity-1" });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        _harness.DispositionService.Verify(service => service.ApplyAsync(It.IsAny<ActivityDispositionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Complete_RelaysTheDispositionServicesRefusal()
    {
        // Arrange
        _harness.AddActivity("activity-1");
        _harness.DispositionService
            .Setup(service => service.ApplyAsync(It.IsAny<ActivityDispositionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActivityDispositionResult.Failure("A disposition is required to complete this activity."));

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CompleteOmnichannelActivityTool(), _harness.Build(), new() { ["id"] = "activity-1" });

        // Assert
        Assert.Equal("Nothing was completed: A disposition is required to complete this activity.", result);
    }

    [Fact]
    public async Task Purge_PurgesTheActivitiesAndReportsTheOnesItSkipped()
    {
        // Arrange
        var pending = _harness.AddActivity("activity-1");
        _harness.AddActivity("activity-2", value => value.Status = ActivityStatus.Completed);
        var onCall = _harness.AddActivity("activity-3");
        _harness.InFlight.Add("activity-3");

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new PurgeOmnichannelActivitiesTool(), _harness.Build(), new()
        {
            ["ids"] = new[] { "activity-1", "activity-2", "activity-3", "missing" },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        Assert.Equal("activity-1", Assert.Single(json.GetProperty("purged").EnumerateArray()).GetString());
        Assert.Equal(3, json.GetProperty("skipped").GetArrayLength());
        Assert.Equal(ActivityStatus.Purged, pending.Status);
        Assert.Equal(AgentToolTestContext.UserId, pending.PurgedById);
        Assert.Equal(OmnichannelToolHarness.Now, pending.PurgedAtUtc);
        Assert.Equal(ActivityStatus.NotStated, onCall.Status);
        Assert.Same(pending, Assert.Single(_harness.UpdatedActivities));
    }

    [Fact]
    public async Task Purge_ChecksPurgeActivityOnEachActivity()
    {
        // Arrange
        _harness.AddActivity("activity-1");
        var services = _harness.Build(authorize: (permission, resource) => permission.Name == OmnichannelConstants.Permissions.PurgeActivity.Name && resource is null);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new PurgeOmnichannelActivitiesTool(), services, new() { ["ids"] = new[] { "activity-1" } });

        // Assert
        Assert.Equal(0, AgentToolTestContext.ReadJson(result).GetProperty("purgedCount").GetInt32());
        Assert.Empty(_harness.UpdatedActivities);
    }
}
