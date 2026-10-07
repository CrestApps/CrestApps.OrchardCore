using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Pins the recordings an activity's page offers to play: the recordings of the activity's calls, found through the
/// activity and through its interaction, listed once, only when playable, and only to a viewer the call recordings
/// page would let hear them.
/// </summary>
public sealed class ActivityCallRecordingsTests
{
    private const string ActivityId = "activity-1";
    private const string InteractionId = "interaction-1";

    [Fact]
    public async Task Lookup_ListsRecordingsFoundByActivityAndByInteraction_Once_OldestFirst()
    {
        // Arrange
        // An AI call handed to an agent names both the activity and the interaction; a routed agent call names only
        // the interaction.
        var handedOff = Playable("rec-ai", agentUserId: null, startedUtc: At(10), interactionId: InteractionId, activityItemId: ActivityId);
        var routed = Playable("rec-agent", agentUserId: "user-1", startedUtc: At(20), interactionId: InteractionId, activityItemId: null);
        var lookup = CreateLookup(byActivity: [handedOff], byInteraction: [routed, handedOff]);

        // Act
        var recordings = await lookup.ListPlayableAsync(ActivityId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["rec-ai", "rec-agent"], recordings.Select(recording => recording.ItemId));
    }

    [Fact]
    public async Task Lookup_LeavesOutRecordingsNotStoredYetOrErased()
    {
        // Arrange
        var stored = Playable("rec-stored", "user-1", At(10), InteractionId, ActivityId);
        var running = Playable("rec-running", "user-1", At(20), InteractionId, null);
        running.StoredUtc = null;
        var erased = Playable("rec-erased", "user-1", At(30), InteractionId, null);
        erased.ErasedUtc = At(40);
        var lookup = CreateLookup(byActivity: [stored], byInteraction: [running, erased]);

        // Act
        var recordings = await lookup.ListPlayableAsync(ActivityId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["rec-stored"], recordings.Select(recording => recording.ItemId));
    }

    [Fact]
    public async Task Lookup_SearchesTheCatalogByTheActivity()
    {
        // Arrange
        var store = new Mock<ICallRecordingStore>();
        CallRecordingQuery asked = null;
        store
            .Setup(value => value.QueryAsync(It.IsAny<CallRecordingQuery>(), It.IsAny<CancellationToken>()))
            .Callback((CallRecordingQuery query, CancellationToken _) => asked = query)
            .ReturnsAsync(CallRecordingPage.Empty);
        var lookup = new ActivityCallRecordingLookup(store.Object, Mock.Of<IInteractionManager>());

        // Act
        var recordings = await lookup.ListPlayableAsync(ActivityId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(recordings);
        Assert.Equal(ActivityId, asked.ActivityItemId);
        Assert.Null(asked.AgentUserId);
        store.Verify(value => value.ListByInteractionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Access_WithoutEitherPermission_HearsNothing()
    {
        // Arrange
        var evaluator = new CallRecordingAccessEvaluator(SharedVoicemailAuthorizationServiceTests.CreateAuthorization().Object);

        // Act
        var access = await evaluator.GetAccessAsync(SharedVoicemailAuthorizationServiceTests.CreatePrincipal());

        // Assert
        Assert.False(access.CanListOwn);
        Assert.False(access.CanListEveryone);
        Assert.False(access.CanHear(Playable("rec-own", "user-1", At(10), null, ActivityId)));
    }

    [Fact]
    public async Task Access_ToOwnRecordings_HearsOnlyTheCallsTheUserTook()
    {
        // Arrange
        var evaluator = new CallRecordingAccessEvaluator(
            SharedVoicemailAuthorizationServiceTests.CreateAuthorization(ContactCenterPermissions.ListenToOwnCallRecordings).Object);

        // Act
        var access = await evaluator.GetAccessAsync(SharedVoicemailAuthorizationServiceTests.CreatePrincipal());

        // Assert
        Assert.True(access.CanListOwn);
        Assert.False(access.CanListEveryone);
        Assert.True(access.CanHear(Playable("rec-own", "user-1", At(10), null, ActivityId)));
        Assert.False(access.CanHear(Playable("rec-other", "user-2", At(10), null, ActivityId)));
        Assert.False(access.CanHear(Playable("rec-ai", null, At(10), null, ActivityId)));
    }

    [Fact]
    public async Task Access_ToAllRecordings_HearsEveryCall()
    {
        // Arrange
        var evaluator = new CallRecordingAccessEvaluator(
            SharedVoicemailAuthorizationServiceTests.CreateAuthorization(ContactCenterPermissions.ListenToAllCallRecordings).Object);

        // Act
        var access = await evaluator.GetAccessAsync(SharedVoicemailAuthorizationServiceTests.CreatePrincipal());

        // Assert
        Assert.True(access.CanListOwn);
        Assert.True(access.CanListEveryone);
        Assert.True(access.CanHear(Playable("rec-other", "user-2", At(10), null, ActivityId)));
        Assert.True(access.CanHear(Playable("rec-ai", null, At(10), null, ActivityId)));
    }

    [Fact]
    public async Task Driver_AViewerWhoMayHearNoRecording_IsShownNothing_AndNothingIsLookedUp()
    {
        // Arrange
        var store = new Mock<ICallRecordingStore>(MockBehavior.Strict);
        var driver = CreateDriver(store.Object, Mock.Of<IInteractionManager>(), granted: []);

        // Act
        var result = await driver.EditAsync(Activity(ActivityStatus.NotStated), Context(OmnichannelConstants.CompleteActivityGroup));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Driver_AnAgentWhoMayHearOnlyTheirOwnCalls_IsShownNothing_WhenTheCallsWereSomeoneElses()
    {
        // Arrange
        var driver = CreateDriver(
            byActivity: [Playable("rec-other", "user-2", At(10), null, ActivityId)],
            granted: [ContactCenterPermissions.ListenToOwnCallRecordings]);

        // Act
        var result = await driver.EditAsync(Activity(ActivityStatus.NotStated), Context(OmnichannelConstants.CompleteActivityGroup));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Driver_WhileCompleting_ListsTheRecordings()
    {
        // Arrange
        var driver = CreateDriver(
            byActivity: [Playable("rec-ai", null, At(10), null, ActivityId)],
            granted: [ContactCenterPermissions.ListenToAllCallRecordings]);

        // Act
        var result = await driver.EditAsync(Activity(ActivityStatus.NotStated), Context(OmnichannelConstants.CompleteActivityGroup));
        var models = await DisplayResultModels.BuildAsync<ActivityCallRecordingsViewModel>(result);

        // Assert
        var item = Assert.Single(Assert.Single(models).Items);
        Assert.Equal("rec-ai", item.Recording.ItemId);
        Assert.Null(item.AgentName);
    }

    [Fact]
    public async Task Driver_OnACompletedActivitysPage_ListsTheRecordings()
    {
        // Arrange
        var driver = CreateDriver(
            byActivity: [Playable("rec-ai", null, At(10), null, ActivityId)],
            granted: [ContactCenterPermissions.ListenToAllCallRecordings]);

        // Act
        var result = await driver.EditAsync(Activity(ActivityStatus.Completed), Context(string.Empty));
        var models = await DisplayResultModels.BuildAsync<ActivityCallRecordingsViewModel>(result);

        // Assert
        Assert.Single(Assert.Single(models).Items);
    }

    [Fact]
    public async Task Driver_OnAnOpenActivitysEditPage_IsShownNothing()
    {
        // Arrange
        var store = new Mock<ICallRecordingStore>(MockBehavior.Strict);
        var driver = CreateDriver(store.Object, Mock.Of<IInteractionManager>(), [ContactCenterPermissions.ListenToAllCallRecordings]);

        // Act
        var result = await driver.EditAsync(Activity(ActivityStatus.NotStated), Context(string.Empty));

        // Assert
        Assert.Null(result);
    }

    private static ActivityCallRecordingLookup CreateLookup(CallRecording[] byActivity, CallRecording[] byInteraction)
    {
        var (store, interactions) = CreateStores(byActivity, byInteraction);

        return new ActivityCallRecordingLookup(store, interactions);
    }

    private static (ICallRecordingStore Store, IInteractionManager Interactions) CreateStores(CallRecording[] byActivity, CallRecording[] byInteraction)
    {
        var store = new Mock<ICallRecordingStore>();
        store
            .Setup(value => value.QueryAsync(It.Is<CallRecordingQuery>(query => query.ActivityItemId == ActivityId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallRecordingPage { Count = byActivity.Length, Entries = byActivity });
        store
            .Setup(value => value.ListByInteractionIdAsync(InteractionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(byInteraction);

        var interactions = new Mock<IInteractionManager>();
        interactions
            .Setup(value => value.FindByActivityIdAsync(ActivityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(byInteraction.Length == 0 ? null : new Interaction { ItemId = InteractionId, ActivityItemId = ActivityId });

        return (store.Object, interactions.Object);
    }

    private static OmnichannelActivityCallRecordingsDisplayDriver CreateDriver(CallRecording[] byActivity, Permission[] granted)
    {
        var (store, interactions) = CreateStores(byActivity, []);

        return CreateDriver(store, interactions, granted);
    }

    private static OmnichannelActivityCallRecordingsDisplayDriver CreateDriver(
        ICallRecordingStore store,
        IInteractionManager interactions,
        Permission[] granted)
    {
        var localClock = new Mock<ILocalClock>();
        localClock
            .Setup(value => value.ConvertToLocalAsync(It.IsAny<DateTimeOffset>()))
            .ReturnsAsync((DateTimeOffset utc) => utc);

        // The session is only read to name agents; the calls these tests list have none, so it is never queried.
        return new OmnichannelActivityCallRecordingsDisplayDriver(
            new ActivityCallRecordingLookup(store, interactions),
            new CallRecordingAccessEvaluator(SharedVoicemailAuthorizationServiceTests.CreateAuthorization(granted).Object),
            new CallRecordingAgentNameResolver(Mock.Of<global::YesSql.ISession>(MockBehavior.Strict), Array.Empty<IDisplayNameProvider>()),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = SharedVoicemailAuthorizationServiceTests.CreatePrincipal() } },
            localClock.Object);
    }

    private static BuildEditorContext Context(string groupId)
        => new(
            Mock.Of<IShape>(),
            groupId,
            isNew: false,
            htmlFieldPrefix: string.Empty,
            Mock.Of<IShapeFactory>(),
            layout: null,
            new PostedFormUpdateModel(null));

    private static OmnichannelActivity Activity(ActivityStatus status)
        => new() { ItemId = ActivityId, Status = status };

    private static CallRecording Playable(string id, string agentUserId, DateTime startedUtc, string interactionId, string activityItemId)
        => new()
        {
            ItemId = id,
            AgentUserId = agentUserId,
            StartedUtc = startedUtc,
            InteractionId = interactionId,
            ActivityItemId = activityItemId,
            StorageReference = "media/" + id,
            StoredUtc = startedUtc.AddMinutes(5),
        };

    private static DateTime At(int minute)
        => new(2026, 10, 7, 12, minute, 0, DateTimeKind.Utc);
}
