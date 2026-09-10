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

    [Fact]
    public void TryMap_WhenSameEventIsReserializedWithDifferentFormatting_ProducesSameIdempotencyKey()
    {
        // Arrange - the same ChannelHold event delivered twice, but with different property order and whitespace, as
        // an Asterisk upgrade or a re-serializing proxy could produce. Deduplication must survive the reformatting.
        const string compactPayload =
            """
            {"type":"ChannelHold","timestamp":"2026-07-10T15:03:00.000Z","application":"crestapps-telephony","channel":{"id":"call-1","state":"Up","caller":{"number":"+15550001000"}}}
            """;

        const string reorderedPayload =
            """
            {
              "channel": {
                "caller": {
                  "number": "+15550001000"
                },
                "state": "Up",
                "id": "call-1"
              },
              "application": "crestapps-telephony",
              "timestamp": "2026-07-10T15:03:00.000Z",
              "type": "ChannelHold"
            }
            """;

        // Act
        var mappedCompact = AsteriskRealtimeVoiceEventMapper.TryMap("Asterisk", compactPayload, out var compactEvent);
        var mappedReordered = AsteriskRealtimeVoiceEventMapper.TryMap("Asterisk", reorderedPayload, out var reorderedEvent);

        // Assert
        Assert.True(mappedCompact);
        Assert.True(mappedReordered);
        Assert.Equal(compactEvent.IdempotencyKey, reorderedEvent.IdempotencyKey);
    }

    [Fact]
    public void TryMap_WhenTwoDistinctSameTypeEventsOccurOnOneCall_ProduceDifferentIdempotencyKeys()
    {
        // Arrange - the same call is placed on hold twice (a legitimate hold/resume/hold cycle). The two ChannelHold
        // events share provider, call id, and type, so a coarse (provider, callId, type) key would wrongly suppress
        // the second one. They differ in timestamp, which the content-based key must keep distinct.
        const string firstHoldPayload =
            """
            {
              "type": "ChannelHold",
              "timestamp": "2026-07-10T15:03:00.000Z",
              "application": "crestapps-telephony",
              "channel": {
                "id": "call-1",
                "state": "Up",
                "caller": {
                  "number": "+15550001000"
                }
              }
            }
            """;

        const string secondHoldPayload =
            """
            {
              "type": "ChannelHold",
              "timestamp": "2026-07-10T15:05:30.000Z",
              "application": "crestapps-telephony",
              "channel": {
                "id": "call-1",
                "state": "Up",
                "caller": {
                  "number": "+15550001000"
                }
              }
            }
            """;

        // Act
        var mappedFirst = AsteriskRealtimeVoiceEventMapper.TryMap("Asterisk", firstHoldPayload, out var firstEvent);
        var mappedSecond = AsteriskRealtimeVoiceEventMapper.TryMap("Asterisk", secondHoldPayload, out var secondEvent);

        // Assert
        Assert.True(mappedFirst);
        Assert.True(mappedSecond);
        Assert.NotEqual(firstEvent.IdempotencyKey, secondEvent.IdempotencyKey);
    }

    [Fact]
    public void TryMap_WhenNumericFieldIsReserializedWithEquivalentValue_ProducesSameIdempotencyKey()
    {
        // Arrange - the same ChannelDestroyed event whose numeric cause code is written as an integer in one delivery
        // and as an equivalent decimal/exponent form in another, as a JSON parse/re-serialize proxy could produce.
        const string integerCausePayload =
            """
            {
              "type": "ChannelDestroyed",
              "timestamp": "2026-07-10T15:03:00.000Z",
              "application": "crestapps-telephony",
              "cause": 16,
              "channel": {
                "id": "call-1",
                "state": "Up"
              }
            }
            """;

        const string decimalCausePayload =
            """
            {
              "type": "ChannelDestroyed",
              "timestamp": "2026-07-10T15:03:00.000Z",
              "application": "crestapps-telephony",
              "cause": 1.6e1,
              "channel": {
                "id": "call-1",
                "state": "Up"
              }
            }
            """;

        // Act
        var mappedInteger = AsteriskRealtimeVoiceEventMapper.TryMap("Asterisk", integerCausePayload, out var integerEvent);
        var mappedDecimal = AsteriskRealtimeVoiceEventMapper.TryMap("Asterisk", decimalCausePayload, out var decimalEvent);

        // Assert
        Assert.True(mappedInteger);
        Assert.True(mappedDecimal);
        Assert.Equal(integerEvent.IdempotencyKey, decimalEvent.IdempotencyKey);
    }

    [Fact]
    public void TryMap_BuildsIdempotencyKeyPrefixedWithProviderAndEventType()
    {
        // Arrange
        const string payload =
            """
            {
              "type": "ChannelHold",
              "timestamp": "2026-07-10T15:03:00.000Z",
              "application": "crestapps-telephony",
              "channel": {
                "id": "call-1",
                "state": "Up"
              }
            }
            """;

        // Act
        var mapped = AsteriskRealtimeVoiceEventMapper.TryMap("Asterisk", payload, out var voiceEvent);

        // Assert
        Assert.True(mapped);
        Assert.StartsWith("Asterisk:ChannelHold:", voiceEvent.IdempotencyKey);
    }
}
