using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskRealtimeVoiceEventMapperTests
{
    [Fact]
    public void TryMap_WhenChannelDestroyedPayloadReceived_ReturnsDisconnectedVoiceEvent()
    {
        // Arrange
        const string payload =
            """
            {
              "type": "ChannelDestroyed",
              "timestamp": "2026-07-10T15:03:00.000Z",
              "application": "crestapps-telephony",
              "cause": 16,
              "cause_txt": "Normal Clearing",
              "channel": {
                "id": "call-1",
                "state": "Up",
                "caller": {
                  "number": "+15550001000"
                },
                "connected": {
                  "number": "+15550002000"
                }
              }
            }
            """;

        // Act
        var mapped = AsteriskRealtimeVoiceEventMapper.TryMap("Asterisk", payload, out var voiceEvent);

        // Assert
        Assert.True(mapped);
        Assert.NotNull(voiceEvent);
        Assert.Equal("call-1", voiceEvent.CallId);
        Assert.Equal(CallState.Disconnected, voiceEvent.State);
        Assert.Equal("+15550001000", voiceEvent.FromAddress);
        Assert.Equal("+15550002000", voiceEvent.ToAddress);
        Assert.Equal("ChannelDestroyed", voiceEvent.EventType);
        Assert.Equal("Normal Clearing", voiceEvent.Metadata["causeText"]);
    }

    [Fact]
    public void TryMap_WhenChannelLeavesBridge_ReturnsUnheldVoiceEvent()
    {
        // Arrange
        const string payload =
            """
            {
              "type": "ChannelLeftBridge",
              "timestamp": "2026-07-10T15:03:00.000Z",
              "application": "crestapps-telephony",
              "bridge": {
                "id": "bridge-1"
              },
              "channel": {
                "id": "call-1",
                "state": "Up",
                "caller": {
                  "number": "+15550001000"
                },
                "connected": {
                  "number": "+15550002000"
                }
              }
            }
            """;

        // Act
        var mapped = AsteriskRealtimeVoiceEventMapper.TryMap("Asterisk", payload, out var voiceEvent);

        // Assert
        Assert.True(mapped);
        Assert.NotNull(voiceEvent);
        Assert.Equal(CallState.Connected, voiceEvent.State);
        Assert.False(voiceEvent.IsOnHold);
        Assert.Equal("ChannelLeftBridge", voiceEvent.EventType);
        Assert.Equal("bridge-1", voiceEvent.Metadata["bridgeId"]);
    }

    [Fact]
    public void TryMap_WhenDownChannelLeavesBridge_DoesNotEmitFalseConnectingState()
    {
        // Arrange
        const string payload =
            """
            {
              "type": "ChannelLeftBridge",
              "timestamp": "2026-07-10T15:03:00.000Z",
              "application": "crestapps-telephony",
              "bridge": {
                "id": "bridge-1"
              },
              "channel": {
                "id": "call-1",
                "state": "Down"
              }
            }
            """;

        // Act
        var mapped = AsteriskRealtimeVoiceEventMapper.TryMap("Asterisk", payload, out var voiceEvent);

        // Assert
        Assert.False(mapped);
        Assert.Null(voiceEvent);
    }

    [Fact]
    public void TryMap_WhenSecondChannelEntersBridge_MarksConference()
    {
        // Arrange
        const string payload =
            """
            {
              "type": "ChannelEnteredBridge",
              "timestamp": "2026-07-13T15:03:00.000Z",
              "bridge": {
                "id": "bridge-1",
                "channels": ["call-1", "call-2"]
              },
              "channel": {
                "id": "call-2",
                "state": "Up"
              }
            }
            """;

        // Act
        var mapped = AsteriskRealtimeVoiceEventMapper.TryMap("Asterisk", payload, out var voiceEvent);

        // Assert
        Assert.True(mapped);
        Assert.NotNull(voiceEvent);
        Assert.True(voiceEvent.IsConference);
        Assert.Equal(2, voiceEvent.ParticipantCount);
        Assert.False(voiceEvent.IsOnHold);
    }

    [Fact]
    public void TryMap_WhenStasisStartCarriesOriginationMarkerInAppArgsOnly_ClassifiesAsOwnedOrigination()
    {
        // Arrange
        var payload =
            $$"""
            {
              "type": "StasisStart",
              "timestamp": "2026-07-10T15:03:00.000Z",
              "application": "crestapps-telephony",
              "args": ["{{AsteriskConstants.OriginationMarkerVariableName}}", "interaction-1", "outbound"],
              "channel": {
                "id": "call-1",
                "state": "Up",
                "caller": {
                  "number": "+15550001000"
                },
                "connected": {
                  "number": "+15550002000"
                }
              }
            }
            """;

        // Act
        var mapped = AsteriskRealtimeVoiceEventMapper.TryMap("Asterisk", payload, out var voiceEvent);

        // Assert
        Assert.True(mapped);
        Assert.NotNull(voiceEvent);
        Assert.True(voiceEvent.IsOwnedOrigination);
        Assert.False(voiceEvent.IsInbound);
    }

    [Fact]
    public void TryMap_WhenStasisStartHasNoOriginationMarker_ClassifiesAsInbound()
    {
        // Arrange
        const string payload =
            """
            {
              "type": "StasisStart",
              "timestamp": "2026-07-10T15:03:00.000Z",
              "application": "crestapps-telephony",
              "args": [],
              "channel": {
                "id": "call-1",
                "state": "Ring",
                "caller": {
                  "number": "+15550001000"
                }
              }
            }
            """;

        // Act
        var mapped = AsteriskRealtimeVoiceEventMapper.TryMap("Asterisk", payload, out var voiceEvent);

        // Assert
        Assert.True(mapped);
        Assert.NotNull(voiceEvent);
        Assert.False(voiceEvent.IsOwnedOrigination);
        Assert.True(voiceEvent.IsInbound);
    }
}
