using System.Net;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Asking the provider who answered an automated call, and carrying the answer to the conversation.
/// </summary>
/// <remarks>
/// A short voicemail greeting plays out under the opening line before any of it is transcribed, so without the
/// provider's answer the call cannot tell a recording from a person until it has already talked over one.
/// </remarks>
public sealed class TelnyxAnsweringMachineDetectionTests
{
    [Theory]
    [InlineData(TelnyxAnsweringMachineDetection.Premium, "premium")]
    [InlineData(TelnyxAnsweringMachineDetection.Standard, "greeting_end")]
    public async Task AnAutomatedCall_AsksTheProviderWhoAnswered(TelnyxAnsweringMachineDetection detection, string expected)
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"call_control_id":"ctrl-1"}}""");
        var client = CreateClient(handler, detection);

        // Act
        await client.OriginateAsync("+15551230000", "+15559870000", AiVoiceState(), TestContext.Current.CancellationToken);

        // Assert
        var request = Assert.Single(handler.Requests);
        Assert.Contains($"\"answering_machine_detection\":\"{expected}\"", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithDetectionOff_TheProviderIsNotAsked()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"call_control_id":"ctrl-1"}}""");
        var client = CreateClient(handler, TelnyxAnsweringMachineDetection.Disabled);

        // Act
        await client.OriginateAsync("+15551230000", "+15559870000", AiVoiceState(), TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain("answering_machine_detection", Assert.Single(handler.Requests).Body, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDetectionVerdict_IsReadFromTheEvent()
    {
        // Arrange
        const string Payload = """
        {
          "data": {
            "event_type": "call.machine.premium.detection.ended",
            "payload": { "call_control_id": "ctrl-1", "result": "machine" }
          }
        }
        """;

        // Act
        TelnyxCallEventParser.TryParse(Payload, out var callEvent);

        // Assert
        Assert.Equal("machine", callEvent.MachineDetectionResult);
    }

    [Fact]
    public void AResultOnAnyOtherEvent_IsNotTakenForAVerdict()
    {
        // Arrange
        // "result" is a common field name, and a verdict on who answered read from the wrong event would send a
        // person to voicemail.
        const string Payload = """
        {
          "data": {
            "event_type": "call.gather.ended",
            "payload": { "call_control_id": "ctrl-1", "result": "machine" }
          }
        }
        """;

        // Act
        TelnyxCallEventParser.TryParse(Payload, out var callEvent);

        // Assert
        Assert.Null(callEvent.MachineDetectionResult);
    }

    [Theory]
    [InlineData("call.machine.premium.detection.ended", "machine", VoiceAgentEventKind.AnswererDetected, VoiceAgentAnswerer.Machine)]
    [InlineData("call.machine.premium.detection.ended", "fax_detected", VoiceAgentEventKind.AnswererDetected, VoiceAgentAnswerer.Machine)]
    [InlineData("call.machine.premium.detection.ended", "human_residence", VoiceAgentEventKind.AnswererDetected, VoiceAgentAnswerer.Person)]
    [InlineData("call.machine.premium.detection.ended", "human_business", VoiceAgentEventKind.AnswererDetected, VoiceAgentAnswerer.Person)]
    [InlineData("call.machine.premium.detection.ended", "not_sure", VoiceAgentEventKind.AnswererDetected, VoiceAgentAnswerer.Unknown)]
    [InlineData("call.machine.detection.ended", "machine", VoiceAgentEventKind.AnswererDetected, VoiceAgentAnswerer.Machine)]
    [InlineData("call.machine.detection.ended", "human", VoiceAgentEventKind.AnswererDetected, VoiceAgentAnswerer.Person)]
    [InlineData("call.machine.premium.greeting.ended", "beep_detected", VoiceAgentEventKind.MachineGreetingEnded, VoiceAgentAnswerer.Unknown)]
    [InlineData("call.machine.greeting.ended", "ended", VoiceAgentEventKind.MachineGreetingEnded, VoiceAgentAnswerer.Unknown)]
    public async Task TheProvidersVerdict_ReachesTheConversation(
        string eventType,
        string result,
        VoiceAgentEventKind expectedKind,
        VoiceAgentAnswerer expectedAnswerer)
    {
        // Arrange
        VoiceAgentEvent handled = null;
        var loop = new Mock<IVoiceAgentConversationLoop>();
        loop.Setup(x => x.HandleAsync(It.IsAny<VoiceAgentEvent>(), It.IsAny<CancellationToken>()))
            .Callback<VoiceAgentEvent, CancellationToken>((voiceEvent, _) => handled = voiceEvent)
            .Returns(Task.CompletedTask);
        var conversationHandler = new TelnyxAiVoiceConversationHandler(loop.Object);

        // Act
        await conversationHandler.HandleAsync(
            new TelnyxCallEvent { EventType = eventType, CallControlId = "ctrl-1", MachineDetectionResult = result },
            AiVoiceState(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(handled);
        Assert.Equal(expectedKind, handled.Kind);
        Assert.Equal(expectedAnswerer, handled.Answerer);
        Assert.Equal("activity-1", handled.ActivityId);
    }

    private static TelnyxOutboundBridgeState AiVoiceState()
        => new()
        {
            Intent = TelnyxOutboundBridgeState.AiVoiceLegIntent,
            ActivityId = "activity-1",
        };

    private static TelnyxVoiceAgentClient CreateClient(HttpMessageHandler handler, TelnyxAnsweringMachineDetection detection)
    {
        var options = new TelnyxOptions
        {
            ApiBaseUrl = "https://api.telnyx.com/v2/",
            ApiKey = "test-api-key",
            ConnectionId = "conn-1",
            AnsweringMachineDetection = detection,
        };

        var apiClient = new TelnyxApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.telnyx.com/v2/") },
            new OptionsWrapper<TelnyxOptions>(options),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance);

        var monitor = new Mock<IOptionsMonitor<TelnyxOptions>>();
        monitor.SetupGet(x => x.CurrentValue).Returns(options);

        return new TelnyxVoiceAgentClient(apiClient, monitor.Object);
    }
}
