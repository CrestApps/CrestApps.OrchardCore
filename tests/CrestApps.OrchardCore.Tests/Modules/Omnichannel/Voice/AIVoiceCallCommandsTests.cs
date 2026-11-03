using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

public sealed class AIVoiceCallCommandsTests
{
    [Fact]
    public async Task SendAsync_ReachesTheSessionHoldingTheCall_UntilItsRegistrationEnds()
    {
        var commands = new AIVoiceCallCommands();
        var received = new List<AIVoiceCallCommand>();

        var registration = commands.Register("act1", command =>
        {
            received.Add(command);

            return Task.FromResult(true);
        });

        Assert.True(await commands.SendAsync("act1", new AIVoiceCallCommand(AIVoiceCallCommandKind.Instruct, "offer 10%")));
        Assert.False(await commands.SendAsync("act2", new AIVoiceCallCommand(AIVoiceCallCommandKind.Instruct, "nobody here")));

        registration.Dispose();

        Assert.False(await commands.SendAsync("act1", new AIVoiceCallCommand(AIVoiceCallCommandKind.TakeOver)));
        Assert.Equal("offer 10%", Assert.Single(received).Text);
    }

    [Fact]
    public async Task SendAsync_IsHandledWhenAnyHandlerActsOnIt()
    {
        var commands = new AIVoiceCallCommands();
        var takeOverSeen = false;

        using var notes = commands.Register("act1", command => Task.FromResult(command.Kind == AIVoiceCallCommandKind.Instruct));
        using var takeOver = commands.Register("act1", command =>
        {
            takeOverSeen = command.Kind == AIVoiceCallCommandKind.TakeOver;

            return Task.FromResult(takeOverSeen);
        });

        Assert.True(await commands.SendAsync("act1", new AIVoiceCallCommand(AIVoiceCallCommandKind.TakeOver)));
        Assert.True(takeOverSeen);
    }

    [Theory]
    [InlineData(ActivityInteractionType.Automated, ActivityStatus.InProgress, false, "Phone", true)]
    [InlineData(ActivityInteractionType.Automated, ActivityStatus.InProgress, true, "Phone", false)]
    [InlineData(ActivityInteractionType.Manual, ActivityStatus.InProgress, false, "Phone", false)]
    [InlineData(ActivityInteractionType.Automated, ActivityStatus.Completed, false, "Phone", false)]
    [InlineData(ActivityInteractionType.Automated, ActivityStatus.InProgress, false, "SMS", false)]
    public void IsLiveAICall_IsTrueOnlyWhileTheAIHoldsAPhoneCall(ActivityInteractionType type, ActivityStatus status, bool escalated, string channel, bool expected)
    {
        var activity = new OmnichannelActivity
        {
            InteractionType = type,
            Status = status,
            AiEscalated = escalated,
            Channel = channel,
        };

        Assert.Equal(expected, VoiceAgentConversationLoop.IsLiveAICall(activity));
    }

    [Fact]
    public void SupervisorNoteGuidance_TellsTheModelToActWithoutMentioningTheSupervisor()
    {
        var guidance = VoiceAgentConversationLoop.SupervisorNoteGuidance("offer the 10% retention discount");

        Assert.Contains("offer the 10% retention discount", guidance);
        Assert.Contains("never mention the supervisor", guidance);
    }
}
