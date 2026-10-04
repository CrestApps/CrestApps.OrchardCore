using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// An AI profile made to answer calls, such as one from the "Answer calls at the front desk" template, had no way to be
/// reached by an inbound call. A call entry point can now route its calls to an AI voice agent.
/// </summary>
public sealed class AIVoiceEntryPointTests
{
    [Fact]
    public void Plan_HandsAnOpenEntryPointsCallsToTheAIAgent()
    {
        // Arrange
        var entryPoint = AIEntryPoint();

        // Act
        var plan = EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen: true);

        // Assert
        Assert.True(plan.RouteToAIAgent);
        Assert.Equal("front-desk-profile", plan.TargetAIProfileId);
        Assert.False(plan.RouteToAgent);
        Assert.Null(plan.TargetQueueId);
    }

    [Theory]
    [InlineData(EntryPointClosedAction.HoldInQueue, EntryPointClosedAction.Voicemail)]
    [InlineData(EntryPointClosedAction.Overflow, EntryPointClosedAction.Voicemail)]
    [InlineData(EntryPointClosedAction.Voicemail, EntryPointClosedAction.Voicemail)]
    [InlineData(EntryPointClosedAction.Reject, EntryPointClosedAction.Reject)]
    public void Plan_SendsAClosedEntryPointsCallsToVoicemailOrRejectsThem(EntryPointClosedAction configured, EntryPointClosedAction expected)
    {
        // Arrange
        var entryPoint = AIEntryPoint();
        entryPoint.ClosedAction = configured;

        // Act
        var plan = EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen: false);

        // Assert
        Assert.False(plan.RouteToAIAgent);
        Assert.False(plan.ShouldQueue);
        Assert.Null(plan.TargetQueueId);
        Assert.Equal(expected, plan.ClosedAction);
    }

    [Fact]
    public async Task Validating_RequiresTheAIProfile()
    {
        // Arrange
        var entryPoint = AIEntryPoint();
        entryPoint.TargetAIProfileId = null;
        var context = new ValidatingContext<ContactCenterEntryPoint>(entryPoint);

        // Act
        await CreateHandler().ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(context.Result.Errors, error => error.MemberNames.Contains(nameof(ContactCenterEntryPoint.TargetAIProfileId)));
    }

    [Fact]
    public async Task Validating_RefusesAnAIAgentForTextsWhenNoFeatureAnswersThem()
    {
        // Arrange
        var entryPoint = AIEntryPoint();
        entryPoint.Channel = OmnichannelConstants.Channels.Sms;
        var context = new ValidatingContext<ContactCenterEntryPoint>(entryPoint);

        // Act
        await CreateHandler().ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(context.Result.Errors, error => error.MemberNames.Contains(nameof(ContactCenterEntryPoint.TargetType)));
    }

    // SMS Omnichannel Automation registers texts as a channel an AI agent answers, so a text entry point may then route
    // to one; the profile is still required.
    [Fact]
    public async Task Validating_AcceptsAnAIAgentForTextsWhenAFeatureAnswersThem()
    {
        // Arrange
        var entryPoint = AIEntryPoint();
        entryPoint.Channel = OmnichannelConstants.Channels.Sms;
        var context = new ValidatingContext<ContactCenterEntryPoint>(entryPoint);

        // Act
        await CreateHandler(OmnichannelConstants.Channels.Sms).ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    [Fact]
    public async Task Validating_RequiresTheAIProfileForTexts()
    {
        // Arrange
        var entryPoint = AIEntryPoint();
        entryPoint.Channel = OmnichannelConstants.Channels.Sms;
        entryPoint.TargetAIProfileId = null;
        var context = new ValidatingContext<ContactCenterEntryPoint>(entryPoint);

        // Act
        await CreateHandler(OmnichannelConstants.Channels.Sms).ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var error = Assert.Single(context.Result.Errors);
        Assert.Contains(nameof(ContactCenterEntryPoint.TargetAIProfileId), error.MemberNames);
        Assert.Equal("Select the AI agent that answers this entry point's texts.", error.ErrorMessage);
    }

    [Fact]
    public async Task Validating_AcceptsACallEntryPointWithAnAIProfile()
    {
        // Arrange
        var context = new ValidatingContext<ContactCenterEntryPoint>(AIEntryPoint());

        // Act
        await CreateHandler().ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    [Fact]
    public async Task Dispatcher_AnswersThroughTheProvidersAnswerer()
    {
        // Arrange
        var answerer = Answerer("Telnyx", answers: true);
        var harness = new DispatcherHarness(answerer.Object);

        // Act
        await harness.Dispatcher.AnswerAsync("telnyx", "call-1", "act1", TestContext.Current.CancellationToken);

        // Assert
        answerer.Verify(candidate => candidate.AnswerAsync("call-1", "act1", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(ActivityStatus.AwaitingCustomerAnswer, harness.Activity.Status);
    }

    [Fact]
    public async Task Dispatcher_FailsTheActivityWhenTheAnswerIsRefused()
    {
        // Arrange
        var harness = new DispatcherHarness(Answerer("Telnyx", answers: false).Object);

        // Act
        await harness.Dispatcher.AnswerAsync("Telnyx", "call-1", "act1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ActivityStatus.Failed, harness.Activity.Status);
        Assert.Equal("ai_agent_answer_refused", harness.Activity.TerminalReasonCode);
        Assert.NotNull(harness.Activity.CompletedUtc);
    }

    [Fact]
    public async Task Dispatcher_FailsTheActivityWhenNoAnswererServesTheProvider()
    {
        // Arrange
        var harness = new DispatcherHarness(Answerer("Other", answers: true).Object);

        // Act
        await harness.Dispatcher.AnswerAsync("Telnyx", "call-1", "act1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ActivityStatus.Failed, harness.Activity.Status);
    }

    private static ContactCenterEntryPoint AIEntryPoint()
        => new()
        {
            ItemId = "front-desk",
            Name = "Front desk",
            Channel = OmnichannelConstants.Channels.Phone,
            TargetType = EntryPointTargetType.AIAgent,
            TargetAIProfileId = "front-desk-profile",
            Enabled = true,
        };

    private static Mock<IInboundAIVoiceAnswerer> Answerer(string providerName, bool answers)
    {
        var answerer = new Mock<IInboundAIVoiceAnswerer>();
        answerer.SetupGet(candidate => candidate.ProviderName).Returns(providerName);
        answerer
            .Setup(candidate => candidate.AnswerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(answers);

        return answerer;
    }

    private static ContactCenterEntryPointHandler CreateHandler(params string[] aiAgentChannels)
    {
        var aiAgentOptions = new EntryPointAIAgentOptions();

        foreach (var channel in aiAgentChannels)
        {
            aiAgentOptions.Channels.Add(channel);
        }

        var addressStore = new Mock<IOmnichannelChannelEndpointStore>();
        addressStore.Setup(store => store.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var entryPointStore = new Mock<IContactCenterEntryPointStore>();
        entryPointStore.Setup(store => store.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        return new ContactCenterEntryPointHandler(
            new Mock<IClock>().Object,
            addressStore.Object,
            entryPointStore.Object,
            Microsoft.Extensions.Options.Options.Create(aiAgentOptions),
            new PassThroughStringLocalizer<ContactCenterEntryPointHandler>());
    }

    private sealed class DispatcherHarness
    {
        public DispatcherHarness(IInboundAIVoiceAnswerer answerer)
        {
            var activities = new Mock<IOmnichannelActivityManager>();
            activities.Setup(manager => manager.FindByIdAsync("act1", It.IsAny<CancellationToken>())).ReturnsAsync(Activity);
            activities
                .Setup(manager => manager.UpdateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);

            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc));

            Dispatcher = new InboundAIVoiceAnswererDispatcher(
                [answerer],
                activities.Object,
                clock.Object,
                NullLogger<InboundAIVoiceAnswererDispatcher>.Instance);
        }

        public OmnichannelActivity Activity { get; } = new() { ItemId = "act1", Status = ActivityStatus.AwaitingCustomerAnswer };

        public InboundAIVoiceAnswererDispatcher Dispatcher { get; }
    }
}
