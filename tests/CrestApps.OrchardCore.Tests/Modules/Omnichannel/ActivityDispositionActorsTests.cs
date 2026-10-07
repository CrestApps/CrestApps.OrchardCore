using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

public sealed class ActivityDispositionActorsTests
{
    [Theory]
    [InlineData(ActivityDispositionSource.Agent, ActivityDispositionActor.User)]
    [InlineData(ActivityDispositionSource.AI, ActivityDispositionActor.AIAgent)]
    [InlineData(ActivityDispositionSource.Provider, ActivityDispositionActor.System)]
    [InlineData(ActivityDispositionSource.Workflow, ActivityDispositionActor.System)]
    [InlineData(ActivityDispositionSource.System, ActivityDispositionActor.System)]
    public void FromRequest_WithoutExplicitActor_MapsTheSource(ActivityDispositionSource source, ActivityDispositionActor expected)
    {
        // Act
        var actor = ActivityDispositionActors.FromRequest(new ActivityDispositionRequest { Source = source });

        // Assert
        Assert.Equal(expected, actor);
    }

    [Theory]
    [InlineData(OmnichannelConstants.NotInServiceSources.Dialer, ActivityDispositionActor.Dialer)]
    [InlineData(OmnichannelConstants.NotInServiceSources.AutomatedCall, ActivityDispositionActor.AIAgent)]
    [InlineData(OmnichannelConstants.NotInServiceSources.Lookup, ActivityDispositionActor.System)]
    public void FromRequest_ForANumberNotInService_NamesWhatFoundIt(string notInServiceSource, ActivityDispositionActor expected)
    {
        // Act
        var actor = ActivityDispositionActors.FromRequest(new ActivityDispositionRequest
        {
            Source = ActivityDispositionSource.Provider,
            NotInServiceSource = notInServiceSource,
        });

        // Assert
        Assert.Equal(expected, actor);
    }

    [Fact]
    public void FromRequest_WithExplicitActor_UsesIt()
    {
        // Act
        var actor = ActivityDispositionActors.FromRequest(new ActivityDispositionRequest
        {
            Source = ActivityDispositionSource.System,
            DispositionedBy = ActivityDispositionActor.Dialer,
        });

        // Assert
        Assert.Equal(ActivityDispositionActor.Dialer, actor);
    }

    [Fact]
    public void Stamp_ForTheAIAgent_RecordsTheActivityProfile()
    {
        // Arrange
        var activity = new OmnichannelActivity { AIProfileId = "profile-1" };

        // Act
        ActivityDispositionActors.Stamp(activity, ActivityDispositionActor.AIAgent);

        // Assert
        Assert.Equal(ActivityDispositionActor.AIAgent, activity.DispositionedBy);
        Assert.Equal("profile-1", activity.DispositionedByAIProfileId);
    }

    [Fact]
    public void Stamp_ForAnotherActor_ClearsTheProfile()
    {
        // Arrange
        var activity = new OmnichannelActivity { AIProfileId = "profile-1", DispositionedByAIProfileId = "stale" };

        // Act
        ActivityDispositionActors.Stamp(activity, ActivityDispositionActor.User);

        // Assert
        Assert.Equal(ActivityDispositionActor.User, activity.DispositionedBy);
        Assert.Null(activity.DispositionedByAIProfileId);
    }

    [Fact]
    public void Resolve_WhenStored_ReturnsTheStoredActor()
    {
        // Arrange: stored as the dialer even though a user id is present.
        var activity = new OmnichannelActivity
        {
            DispositionedBy = ActivityDispositionActor.Dialer,
            CompletedById = "user-1",
        };

        // Act & Assert
        Assert.Equal(ActivityDispositionActor.Dialer, ActivityDispositionActors.Resolve(activity));
    }

    [Fact]
    public void Resolve_ForAnOldAutomatedActivity_InfersTheAIAgent()
    {
        // Arrange: automated SMS completions used to record the assignee as the completing user.
        var activity = new OmnichannelActivity
        {
            InteractionType = ActivityInteractionType.Automated,
            CompletedById = "user-1",
            AIProfileId = "profile-1",
        };

        // Act & Assert
        Assert.Equal(ActivityDispositionActor.AIAgent, ActivityDispositionActors.Resolve(activity));
        Assert.Equal("profile-1", ActivityDispositionActors.ResolveAIProfileId(activity));
    }

    [Fact]
    public void Resolve_ForAnOldManualActivityWithACompletingUser_InfersTheUser()
    {
        // Arrange
        var activity = new OmnichannelActivity
        {
            InteractionType = ActivityInteractionType.Manual,
            Source = ActivitySources.PreviewDial,
            CompletedById = "user-1",
        };

        // Act & Assert
        Assert.Equal(ActivityDispositionActor.User, ActivityDispositionActors.Resolve(activity));
        Assert.Null(ActivityDispositionActors.ResolveAIProfileId(activity));
    }

    [Theory]
    [InlineData(ActivitySources.Dialer)]
    [InlineData(ActivitySources.PreviewDial)]
    [InlineData(ActivitySources.PowerDial)]
    [InlineData(ActivitySources.ProgressiveDial)]
    [InlineData(ActivitySources.PredictiveDial)]
    public void Resolve_ForAnOldDialerActivityWithNoUser_InfersTheDialer(string source)
    {
        // Arrange
        var activity = new OmnichannelActivity { Source = source };

        // Act & Assert
        Assert.Equal(ActivityDispositionActor.Dialer, ActivityDispositionActors.Resolve(activity));
    }

    [Theory]
    [InlineData(ActivitySources.Manual)]
    [InlineData(ActivitySources.Inbound)]
    [InlineData(ActivitySources.Callback)]
    [InlineData(null)]
    public void Resolve_ForAnOldNonDialerActivityWithNoUser_InfersTheSystem(string source)
    {
        // Arrange
        var activity = new OmnichannelActivity { Source = source };

        // Act & Assert
        Assert.Equal(ActivityDispositionActor.System, ActivityDispositionActors.Resolve(activity));
    }

    [Fact]
    public void ResolveAIProfileId_PrefersTheStampedProfile()
    {
        // Arrange
        var activity = new OmnichannelActivity
        {
            DispositionedBy = ActivityDispositionActor.AIAgent,
            DispositionedByAIProfileId = "stamped",
            AIProfileId = "current",
        };

        // Act & Assert
        Assert.Equal("stamped", ActivityDispositionActors.ResolveAIProfileId(activity));
    }
}
