using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Endpoints.Api;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

public sealed class AIActivitiesApiTests
{
    private static readonly DateTime _now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("+15551234567", true)]
    [InlineData(" +442071234567 ", true)]
    [InlineData("5551234567", false)]
    [InlineData("+0555123", false)]
    [InlineData("+1555-123-4567", false)]
    [InlineData(null, false)]
    public void IsPhoneNumber_TakesE164Only(string value, bool expected)
        => Assert.Equal(expected, AIActivitiesApiEndpoints.IsPhoneNumber(value));

    [Theory]
    [InlineData(ActivityStatus.Pending, true)]
    [InlineData(ActivityStatus.Scheduled, true)]
    [InlineData(ActivityStatus.NotStated, true)]
    [InlineData(ActivityStatus.InProgress, false)]
    [InlineData(ActivityStatus.Dialing, false)]
    [InlineData(ActivityStatus.Completed, false)]
    public void CanCancel_OnlyBeforeItStarts(ActivityStatus status, bool expected)
        => Assert.Equal(expected, AIActivitiesApiEndpoints.CanCancel(status));

    [Fact]
    public async Task StartAsync_MakesAnAutomatedActivityWithItsContactVariablesAndSchedule()
    {
        // Arrange
        OmnichannelActivity created = null;
        var manager = new Mock<IOmnichannelActivityManager>();
        manager.Setup(x => x.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => new OmnichannelActivity { ItemId = "a-1" });
        manager.Setup(x => x.CreateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<CancellationToken>()))
            .Callback<OmnichannelActivity, CancellationToken>((activity, _) => created = activity)
            .Returns(ValueTask.CompletedTask);
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(x => x.GetAsync("c-1", It.IsAny<VersionOptions>())).ReturnsAsync(new ContentItem { ContentItemId = "c-1", ContentType = "Contact" });
        var starter = new AIActivityStarter(manager.Object, contentManager.Object, Mock.Of<IClock>(clock => clock.UtcNow == _now));

        // Act
        var activity = await starter.StartAsync(new AIActivityStartRequest
        {
            Channel = "sms",
            AIProfileId = "p-1",
            Destination = " +15551234567 ",
            ContactContentItemId = "c-1",
            Variables = [new("order", "42")],
            Source = ActivitySources.Api,
            ScheduledUtc = _now.AddHours(2),
        });

        // Assert
        Assert.Same(activity, created);
        Assert.Equal(ActivityKind.Sms, activity.Kind);
        Assert.Equal(OmnichannelConstants.Channels.Sms, activity.Channel);
        Assert.Equal(ActivityInteractionType.Automated, activity.InteractionType);
        Assert.Equal(ActivitySources.Api, activity.Source);
        Assert.Equal("+15551234567", activity.PreferredDestination);
        Assert.Equal("c-1", activity.ContactContentItemId);
        Assert.Equal(ContactResolutionStatus.Resolved, activity.ContactResolutionStatus);
        Assert.Equal(_now.AddHours(2), activity.ScheduledUtc);
        Assert.Equal("42", activity.GetConversationVariables()["order"]);
    }

    [Fact]
    public async Task StartAsync_StartsNowWhenTheTimeHasPassed()
    {
        // Arrange
        var manager = new Mock<IOmnichannelActivityManager>();
        manager.Setup(x => x.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => new OmnichannelActivity());
        var starter = new AIActivityStarter(manager.Object, Mock.Of<IContentManager>(), Mock.Of<IClock>(clock => clock.UtcNow == _now));

        // Act
        var activity = await starter.StartAsync(new AIActivityStartRequest { AIProfileId = "p-1", Destination = "+15551234567", ScheduledUtc = _now.AddDays(-1) });

        // Assert
        Assert.Equal(_now, activity.ScheduledUtc);
        Assert.Equal(ActivityKind.Call, activity.Kind);
    }
}
