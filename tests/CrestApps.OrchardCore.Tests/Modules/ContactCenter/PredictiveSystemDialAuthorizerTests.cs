using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// A dial that names no agent is placed only for an enabled over-dialing Predictive profile, for a campaign call the
/// attempt service recorded as over-dialed and that still has no agent.
/// </summary>
public sealed class PredictiveSystemDialAuthorizerTests
{
    private const string ProfileId = "profile-1";
    private const string CampaignQueueId = "__campaign-queue__campaign-1";

    [Fact]
    public async Task IsAuthorizedAsync_ForAnOverDialedCampaignCall_ReturnsTrue()
    {
        // Act
        var authorized = await AuthorizeAsync();

        // Assert
        Assert.True(authorized);
    }

    [Theory]
    [InlineData(DialerMode.Power, PredictivePacingModel.ReservedPerCall, true)]
    [InlineData(DialerMode.Predictive, PredictivePacingModel.ReservedPerCall, true)]
    [InlineData(DialerMode.Predictive, PredictivePacingModel.OverDial, false)]
    public async Task IsAuthorizedAsync_ForAProfileThatIsNotAnEnabledOverDialingPredictiveProfile_ReturnsFalse(DialerMode mode, PredictivePacingModel model, bool enabled)
    {
        // Act
        var authorized = await AuthorizeAsync(configureProfile: profile =>
        {
            profile.Mode = mode;
            profile.PredictivePacingModel = model;
            profile.Enabled = enabled;
        });

        // Assert
        Assert.False(authorized);
    }

    [Fact]
    public async Task IsAuthorizedAsync_WhenTheProfileIsMissing_ReturnsFalse()
    {
        // Act
        var authorized = await AuthorizeAsync(profileExists: false);

        // Assert
        Assert.False(authorized);
    }

    [Fact]
    public async Task IsAuthorizedAsync_WhenTheCommandCarriesAReservation_ReturnsFalse()
    {
        // Act
        var authorized = await AuthorizeAsync(configureCommand: command => command.ReservationId = "reservation-1");

        // Assert
        Assert.False(authorized);
    }

    [Theory]
    [InlineData("queue-1")]
    [InlineData(null)]
    public async Task IsAuthorizedAsync_WhenTheQueueIsNotACampaignQueue_ReturnsFalse(string queueId)
    {
        // Act
        var authorized = await AuthorizeAsync(configureRequest: request => request.QueueId = queueId);

        // Assert
        Assert.False(authorized);
    }

    [Fact]
    public async Task IsAuthorizedAsync_ForAQueuedCallback_ReturnsFalse()
    {
        // Act
        var authorized = await AuthorizeAsync(configureCommand: command => command.DialerProfileId = QueueCallbackDialerProfile.Id);

        // Assert
        Assert.False(authorized);
    }

    [Fact]
    public async Task IsAuthorizedAsync_WhenTheInteractionAlreadyHasAnAgent_ReturnsFalse()
    {
        // Act
        var authorized = await AuthorizeAsync(configureInteraction: interaction => interaction.AgentId = "agent-1");

        // Assert
        Assert.False(authorized);
    }

    [Fact]
    public async Task IsAuthorizedAsync_WhenTheInteractionWasNotPlacedWithoutAnAgent_ReturnsFalse()
    {
        // Act
        var authorized = await AuthorizeAsync(configureInteraction: interaction => interaction.TechnicalMetadata.Remove(DialerCallMetadata.PacingModelKey));

        // Assert
        Assert.False(authorized);
    }

    [Fact]
    public async Task IsAuthorizedAsync_WhenTheInteractionBelongsToAnotherActivity_ReturnsFalse()
    {
        // Act
        var authorized = await AuthorizeAsync(configureInteraction: interaction => interaction.ActivityItemId = "activity-2");

        // Assert
        Assert.False(authorized);
    }

    [Fact]
    public async Task IsAuthorizedAsync_WhenTheInteractionIsMissing_ReturnsFalse()
    {
        // Act: the cycle that staged the call lost its commit, so the call it staged does not exist.
        var authorized = await AuthorizeAsync(interactionExists: false);

        // Assert
        Assert.False(authorized);
    }

    [Fact]
    public async Task IsAuthorizedAsync_WhenTheRequestNamesAnAgent_ReturnsFalse()
    {
        // Act
        var authorized = await AuthorizeAsync(configureRequest: request => request.AgentId = "agent-1");

        // Assert
        Assert.False(authorized);
    }

    private static async Task<bool> AuthorizeAsync(
        Action<DialerProfile> configureProfile = null,
        Action<ProviderCommand> configureCommand = null,
        Action<ContactCenterDialRequest> configureRequest = null,
        Action<Interaction> configureInteraction = null,
        bool profileExists = true,
        bool interactionExists = true)
    {
        var profile = new DialerProfile
        {
            ItemId = ProfileId,
            Mode = DialerMode.Predictive,
            PredictivePacingModel = PredictivePacingModel.OverDial,
            Enabled = true,
        };
        configureProfile?.Invoke(profile);

        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-1",
            Direction = InteractionDirection.Outbound,
        };
        DialerCallMetadata.StampDial(interaction, profile, attemptNumber: 1);
        DialerCallMetadata.MarkOverDialed(interaction);
        configureInteraction?.Invoke(interaction);

        var command = new ProviderCommand
        {
            CommandId = "interaction-1",
            CommandType = ProviderCommandType.Dial,
            DialerProfileId = ProfileId,
            InteractionId = "interaction-1",
            ActivityItemId = "activity-1",
        };
        configureCommand?.Invoke(command);

        var request = new ContactCenterDialRequest
        {
            ActivityId = "activity-1",
            InteractionId = "interaction-1",
            QueueId = CampaignQueueId,
            Destination = "+15551112222",
        };
        configureRequest?.Invoke(request);

        var profileReader = new Mock<IDialerProfileReader>();
        profileReader
            .Setup(reader => reader.FindByIdAsync(ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profileExists ? profile : null);

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interactionExists ? interaction : null);

        var authorizer = new PredictiveSystemDialAuthorizer(
            profileReader.Object,
            interactionManager.Object,
            NullLogger<PredictiveSystemDialAuthorizer>.Instance);

        return await authorizer.IsAuthorizedAsync(command, request, TestContext.Current.CancellationToken);
    }
}
