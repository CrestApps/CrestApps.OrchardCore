using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.Extensions.Localization;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed class ActivityHandlerDescriberTests
{
    [Fact]
    public async Task DescribeHandlerAsync_ForAnAutomatedActivity_NamesTheAIProfileAndCampaign()
    {
        // Arrange
        var describer = CreateDescriber(out _, out _);
        var model = new ActivityHandlerViewModel();
        var activity = new OmnichannelActivity
        {
            InteractionType = ActivityInteractionType.Automated,
            Source = ActivitySources.Automatic,
            AIProfileId = "profile-1",
            CampaignId = "campaign-1",
        };

        // Act
        await describer.DescribeHandlerAsync(model, activity, assignedToName: null);

        // Assert
        Assert.Equal("Automated (AI)", model.InteractionTypeName);
        Assert.Equal("Automatic", model.SourceName);
        Assert.Equal("Sales Assistant", model.AIProfileName);
        Assert.Equal("Spring Renewals", model.CampaignName);
        Assert.Null(model.DialerProfileName);
        Assert.Null(model.AssignedToName);
    }

    [Fact]
    public async Task DescribeHandlerAsync_ForADialerActivity_NamesTheDialerProfileAndNotTheAIProfile()
    {
        // Arrange
        var describer = CreateDescriber(out _, out _);
        var model = new ActivityHandlerViewModel();
        var activity = new OmnichannelActivity
        {
            InteractionType = ActivityInteractionType.Manual,
            Source = ActivitySources.PreviewDial,
            DialerProfileId = "dialer-1",
            AIProfileId = "profile-1",
            AssignedToUsername = "agent.login",
        };

        // Act
        await describer.DescribeHandlerAsync(model, activity, assignedToName: null);

        // Assert
        Assert.Equal("Manual", model.InteractionTypeName);
        Assert.Equal("Preview dialer", model.SourceName);
        Assert.Equal("Morning Preview", model.DialerProfileName);
        Assert.Null(model.AIProfileName);

        // Without a resolved display name, the stored username still says who the activity is assigned to.
        Assert.Equal("agent.login", model.AssignedToName);
    }

    [Fact]
    public async Task DescribeHandlerAsync_ReadsEachCatalogOnceForTheWholeList()
    {
        // Arrange
        var describer = CreateDescriber(out var profileManager, out var dialerContributor);

        // Act
        for (var i = 0; i < 5; i++)
        {
            await describer.DescribeHandlerAsync(
                new ActivityHandlerViewModel(),
                new OmnichannelActivity
                {
                    InteractionType = i % 2 == 0 ? ActivityInteractionType.Automated : ActivityInteractionType.Manual,
                    Source = i % 2 == 0 ? ActivitySources.Automatic : ActivitySources.PowerDial,
                    AIProfileId = "profile-1",
                    DialerProfileId = "dialer-1",
                },
                assignedToName: null);
        }

        // Assert
        profileManager.Verify(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
        profileManager.Verify(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        dialerContributor.Verify(contributor => contributor.GetProfilesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDispositionedByNameAsync_ForAUser_UsesTheDisplayName()
    {
        // Arrange
        var describer = CreateDescriber(out _, out _);
        var activity = new OmnichannelActivity
        {
            DispositionedBy = ActivityDispositionActor.User,
            CompletedById = "user-1",
            CompletedByUsername = "agent.login",
        };

        // Act & Assert
        Assert.Equal("Jane Agent", await describer.GetDispositionedByNameAsync(activity, "Jane Agent"));
        Assert.Equal("agent.login", await describer.GetDispositionedByNameAsync(activity, completedByName: null));
    }

    [Fact]
    public async Task GetDispositionedByNameAsync_ForTheAIAgentOnACall_NamesTheVoiceAgentAndProfile()
    {
        // Arrange
        var describer = CreateDescriber(out _, out _);
        var activity = new OmnichannelActivity
        {
            Channel = OmnichannelConstants.Channels.Phone,
            DispositionedBy = ActivityDispositionActor.AIAgent,
            DispositionedByAIProfileId = "profile-1",
        };

        // Act
        var name = await describer.GetDispositionedByNameAsync(activity, completedByName: null);

        // Assert
        Assert.Equal("AI voice agent (profile: Sales Assistant)", name);
    }

    [Fact]
    public async Task GetDispositionedByNameAsync_ForTheAIAgentOverSms_NamesTheAgentAndProfile()
    {
        // Arrange
        var describer = CreateDescriber(out _, out _);

        // An automated SMS completed before the actor was stored, which recorded its assignee as the completing user.
        var activity = new OmnichannelActivity
        {
            Channel = OmnichannelConstants.Channels.Sms,
            InteractionType = ActivityInteractionType.Automated,
            AIProfileId = "profile-1",
            CompletedById = "user-1",
        };

        // Act
        var name = await describer.GetDispositionedByNameAsync(activity, "Jane Agent");

        // Assert
        Assert.Equal("AI agent (profile: Sales Assistant)", name);
    }

    [Fact]
    public async Task GetDispositionedByNameAsync_ForTheAIAgentWithAnUnknownProfile_NamesTheAgentAlone()
    {
        // Arrange
        var describer = CreateDescriber(out _, out _);
        var activity = new OmnichannelActivity
        {
            Channel = OmnichannelConstants.Channels.Phone,
            DispositionedBy = ActivityDispositionActor.AIAgent,
            DispositionedByAIProfileId = "deleted-profile",
        };

        // Act & Assert
        Assert.Equal("AI voice agent", await describer.GetDispositionedByNameAsync(activity, completedByName: null));
    }

    [Fact]
    public async Task GetDispositionedByNameAsync_ForTheDialer_SaysItWasAutomatic()
    {
        // Arrange
        var describer = CreateDescriber(out _, out _);
        var activity = new OmnichannelActivity { DispositionedBy = ActivityDispositionActor.Dialer };

        // Act & Assert
        Assert.Equal("Dialer (automatic)", await describer.GetDispositionedByNameAsync(activity, completedByName: null));
    }

    [Fact]
    public async Task GetDispositionedByNameAsync_ForTheSystem_SaysItWasAutomatic()
    {
        // Arrange
        var describer = CreateDescriber(out _, out _);
        var activity = new OmnichannelActivity { DispositionedBy = ActivityDispositionActor.System };

        // Act & Assert
        Assert.Equal("Automatically (system)", await describer.GetDispositionedByNameAsync(activity, completedByName: null));
    }

    [Fact]
    public async Task DescribeDispositionAsync_FillsTheActorAndItsName()
    {
        // Arrange
        var describer = CreateDescriber(out _, out _);
        var model = new ActivityHandlerViewModel();
        var activity = new OmnichannelActivity { Source = ActivitySources.ProgressiveDial };

        // Act
        await describer.DescribeDispositionAsync(model, activity, completedByName: null);

        // Assert
        Assert.Equal(ActivityDispositionActor.Dialer, model.DispositionedBy);
        Assert.Equal("Dialer (automatic)", model.DispositionedByName);
        Assert.Equal("Progressive dialer", model.SourceName);
    }

    private static ActivityHandlerDescriber CreateDescriber(
        out Mock<IAIProfileManager> profileManager,
        out Mock<IActivityDialerContributor> dialerContributor)
    {
        profileManager = new Mock<IAIProfileManager>();
        profileManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new AIProfile { ItemId = "profile-1", Name = "sales-assistant", DisplayText = "Sales Assistant" },
                new AIProfile { ItemId = "profile-2", Name = "support" },
            });

        var campaigns = new Mock<ICatalog<OmnichannelCampaign>>();
        campaigns
            .Setup(catalog => catalog.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new OmnichannelCampaign { ItemId = "campaign-1", DisplayText = "Spring Renewals" } });

        dialerContributor = new Mock<IActivityDialerContributor>();
        dialerContributor
            .Setup(contributor => contributor.GetProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ActivityDialerProfileDescriptor { ProfileId = "dialer-1", DisplayName = "Morning Preview" } });

        return new ActivityHandlerDescriber(
            [profileManager.Object],
            campaigns.Object,
            [dialerContributor.Object],
            new PassThroughStringLocalizer());
    }

    private sealed class PassThroughStringLocalizer : IStringLocalizer<ActivityHandlerDescriber>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
