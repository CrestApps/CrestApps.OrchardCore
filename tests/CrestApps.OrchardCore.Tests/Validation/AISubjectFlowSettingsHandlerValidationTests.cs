using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Handlers;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;

namespace CrestApps.OrchardCore.Tests.Validation;

/// <summary>
/// Pins the automated subject flow rules to a handler registered with the AI feature, so a tenant without AI is
/// never held to rules it cannot satisfy while a tenant with AI is held to them on every write path.
/// </summary>
public class AISubjectFlowSettingsHandlerValidationTests
{
    [Fact]
    public async Task ValidatingAsync_WhenTheFlowIsNotAutomated_AppliesNoRules()
    {
        // Arrange
        var flow = new SubjectFlowSettings
        {
            InteractionType = ActivityInteractionType.Manual,
        };

        var profileManager = new Mock<IAIProfileManager>(MockBehavior.Strict);
        var context = new ValidatingContext<SubjectFlowSettings>(flow);

        // Act
        await CreateHandler(profileManager.Object).ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(context.Result.Succeeded);
        profileManager.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ValidatingAsync_WhenTheAutomatedFlowIsWellFormed_Succeeds()
    {
        // Act
        var context = await ValidateAsync(CreateValidAutomatedFlow(), CreateChatProfile("profile-1", "Say hello."));

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    [Fact]
    public async Task ValidatingAsync_WhenTheSubjectGoalIsMissing_Fails()
    {
        // Arrange
        var flow = CreateValidAutomatedFlow();
        flow.SubjectGoal = null;

        // Act
        var context = await ValidateAsync(flow, CreateChatProfile("profile-1", "Say hello."));

        // Assert
        AssertFailedFor(context, nameof(SubjectFlowSettings.SubjectGoal));
    }

    [Fact]
    public async Task ValidatingAsync_WhenTheProfileIsMissing_Fails()
    {
        // Arrange
        var flow = CreateValidAutomatedFlow();
        flow.ProfileId = null;

        // Act
        var context = await ValidateAsync(flow, profile: null);

        // Assert
        AssertFailedFor(context, nameof(SubjectFlowSettings.ProfileId));
    }

    [Fact]
    public async Task ValidatingAsync_WhenTheProfileCannotBeResolved_Fails()
    {
        // Act
        var context = await ValidateAsync(CreateValidAutomatedFlow(), profile: null);

        // Assert
        AssertFailedFor(context, nameof(SubjectFlowSettings.ProfileId));
    }

    [Fact]
    public async Task ValidatingAsync_WhenTheProfileIsNotAChatProfile_Fails()
    {
        // Arrange
        var profile = CreateChatProfile("profile-1", "Say hello.");
        profile.Type = AIProfileType.TemplatePrompt;

        // Act
        var context = await ValidateAsync(CreateValidAutomatedFlow(), profile);

        // Assert
        AssertFailedFor(context, nameof(SubjectFlowSettings.ProfileId));
    }

    [Fact]
    public async Task ValidatingAsync_WhenTheProfileHasNoInitialPrompt_Fails()
    {
        // Act
        var context = await ValidateAsync(CreateValidAutomatedFlow(), CreateChatProfile("profile-1", initialPrompt: null));

        // Assert
        AssertFailedFor(context, nameof(SubjectFlowSettings.ProfileId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ValidatingAsync_WhenTheNoResponseTimeoutIsNotPositive_Fails(int minutes)
    {
        // Arrange
        var flow = CreateValidAutomatedFlow();
        flow.NoResponseTimeoutInMinutes = minutes;

        // Act
        var context = await ValidateAsync(flow, CreateChatProfile("profile-1", "Say hello."));

        // Assert
        AssertFailedFor(context, nameof(SubjectFlowSettings.NoResponseTimeoutInMinutes));
    }

    [Fact]
    public async Task ValidatingAsync_WhenTheSmsResponseDelayIsNegative_Fails()
    {
        // Arrange
        var flow = CreateValidAutomatedFlow();
        flow.SmsResponseDelayInSeconds = -1;

        // Act
        var context = await ValidateAsync(flow, CreateChatProfile("profile-1", "Say hello."));

        // Assert
        AssertFailedFor(context, nameof(SubjectFlowSettings.SmsResponseDelayInSeconds));
    }

    private static async Task<ValidatingContext<SubjectFlowSettings>> ValidateAsync(SubjectFlowSettings flow, AIProfile profile)
    {
        var profileManager = new Mock<IAIProfileManager>();

        profileManager
            .Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(
                profile is not null && profile.ItemId == id
                    ? profile
                    : null));

        var context = new ValidatingContext<SubjectFlowSettings>(flow);

        await CreateHandler(profileManager.Object).ValidatingAsync(context, TestContext.Current.CancellationToken);

        return context;
    }

    private static void AssertFailedFor(ValidatingContext<SubjectFlowSettings> context, string memberName)
    {
        Assert.False(context.Result.Succeeded);
        Assert.Contains(context.Result.Errors, error => error.MemberNames.Contains(memberName));
    }

    private static AISubjectFlowSettingsHandler CreateHandler(IAIProfileManager profileManager)
        => new(profileManager, new PassThroughStringLocalizer<AISubjectFlowSettingsHandler>());

    private static AIProfile CreateChatProfile(string id, string initialPrompt)
    {
        var profile = new AIProfile
        {
            ItemId = id,
            Type = AIProfileType.Chat,
        };

        profile.Put(new AIProfileMetadata
        {
            InitialPrompt = initialPrompt,
        });

        return profile;
    }

    private static SubjectFlowSettings CreateValidAutomatedFlow()
    {
        return new SubjectFlowSettings
        {
            SubjectContentType = "SupportCase",
            CampaignId = "campaign-1",
            Channel = OmnichannelConstants.Channels.Sms,
            ChannelEndpointId = "endpoint-1",
            InteractionType = ActivityInteractionType.Automated,
            SubjectGoal = "Confirm the appointment.",
            ProfileId = "profile-1",
            NoResponseTimeoutInMinutes = 30,
            SmsResponseDelayInSeconds = 5,
        };
    }
}
