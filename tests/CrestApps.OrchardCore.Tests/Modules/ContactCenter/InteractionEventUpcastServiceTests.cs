using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Gates the conversion of a persisted Contact Center domain event to the schema version the running code
/// understands. A durable event log outlives the code that wrote it, so the failure this guards against is not
/// an exception but a success: today's type deserializes yesterday's JSON without complaint and substitutes
/// defaults for whatever moved.
/// </summary>
public sealed class InteractionEventUpcastServiceTests
{
    private const string PresenceChanged = "presence-changed";
    private const string OfferDeclined = "offer-declined";

    [Fact]
    public void Upcast_WhenTheEventIsAlreadyCurrent_LeavesThePayloadUntouched()
    {
        // Arrange
        var service = new DefaultInteractionEventUpcastService([new RenameStep(1, PresenceChanged)], 1);

        var interactionEvent = new InteractionEvent
        {
            ItemId = "event-1",
            EventType = PresenceChanged,
            SchemaVersion = 1,
            Data = """{"Old":"kept"}""",
        };

        // Act
        service.Upcast(interactionEvent);

        // Assert
        Assert.Equal("""{"Old":"kept"}""", interactionEvent.Data);
        Assert.Equal(1, interactionEvent.SchemaVersion);
    }

    [Fact]
    public void Upcast_WhenTheEventIsSeveralVersionsBehind_AppliesEveryStepInOrder()
    {
        // Arrange
        var service = new DefaultInteractionEventUpcastService(
            [
                new AppendStep(1, PresenceChanged, "first"),
                new AppendStep(2, PresenceChanged, "second"),
                new AppendStep(3, PresenceChanged, "third"),
            ],
            4);

        var interactionEvent = new InteractionEvent
        {
            ItemId = "event-1",
            EventType = PresenceChanged,
            SchemaVersion = 1,
            Data = """{"steps":""}""",
        };

        // Act
        service.Upcast(interactionEvent);

        // Assert
        var steps = JsonNode.Parse(interactionEvent.Data)["steps"].GetValue<string>();

        Assert.Equal("first|second|third", steps);
        Assert.Equal(4, interactionEvent.SchemaVersion);
    }

    [Fact]
    public void Upcast_WhenTheEventEntersMidChain_OnlyAppliesTheRemainingSteps()
    {
        // Arrange
        var service = new DefaultInteractionEventUpcastService(
            [
                new AppendStep(1, PresenceChanged, "first"),
                new AppendStep(2, PresenceChanged, "second"),
                new AppendStep(3, PresenceChanged, "third"),
            ],
            4);

        var interactionEvent = new InteractionEvent
        {
            ItemId = "event-1",
            EventType = PresenceChanged,
            SchemaVersion = 3,
            Data = """{"steps":""}""",
        };

        // Act
        service.Upcast(interactionEvent);

        // Assert
        Assert.Equal("third", JsonNode.Parse(interactionEvent.Data)["steps"].GetValue<string>());
    }

    [Fact]
    public void Upcast_WhenAVersionStepHasNoUpcaster_FailsAndNamesTheGap()
    {
        // Arrange
        // The step from 2 to 3 is missing. Returning the payload unconverted here is precisely the silent
        // failure the whole mechanism exists to prevent, so the read has to fail instead.
        var service = new DefaultInteractionEventUpcastService(
            [
                new AppendStep(1, PresenceChanged, "first"),
                new AppendStep(3, PresenceChanged, "third"),
            ],
            4);

        var interactionEvent = new InteractionEvent
        {
            ItemId = "event-1",
            EventType = PresenceChanged,
            SchemaVersion = 1,
            Data = """{"steps":""}""",
        };

        // Act
        var exception = Assert.Throws<InteractionEventUpcastException>(() => service.Upcast(interactionEvent));

        // Assert
        Assert.Contains("from schema version 2 to 3", exception.Message, StringComparison.Ordinal);
        Assert.Contains(PresenceChanged, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Upcast_WhenTheEventWasWrittenByANewerRelease_RefusesToReadIt()
    {
        // Arrange
        // This is the rolling-upgrade case: the new node is already writing version 2 while the old node is
        // still serving traffic. The old node cannot convert forwards, so the only honest outcome is to refuse
        // the record rather than deserialize a shape it does not know into the shape it does.
        var service = new DefaultInteractionEventUpcastService([], 1);

        var interactionEvent = new InteractionEvent
        {
            ItemId = "event-1",
            EventType = PresenceChanged,
            SchemaVersion = 2,
            Data = """{"new":"shape"}""",
        };

        // Act
        var exception = Assert.Throws<InteractionEventUpcastException>(() => service.Upcast(interactionEvent));

        // Assert
        Assert.Contains("written by a newer release", exception.Message, StringComparison.Ordinal);
        Assert.Equal("""{"new":"shape"}""", interactionEvent.Data);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Upcast_WhenTheStoredVersionIsMissing_TreatsTheEventAsTheFirstVersion(int storedVersion)
    {
        // Arrange
        // A row written before the field existed reads as zero. Treating it as current would skip every
        // conversion it actually needs.
        var service = new DefaultInteractionEventUpcastService([new AppendStep(1, PresenceChanged, "first")], 2);

        var interactionEvent = new InteractionEvent
        {
            ItemId = "event-1",
            EventType = PresenceChanged,
            SchemaVersion = storedVersion,
            Data = """{"steps":""}""",
        };

        // Act
        service.Upcast(interactionEvent);

        // Assert
        Assert.Equal("first", JsonNode.Parse(interactionEvent.Data)["steps"].GetValue<string>());
        Assert.Equal(2, interactionEvent.SchemaVersion);
    }

    [Fact]
    public void Upcast_WhenAnUpcasterDeclaresAnEventType_PrefersItOverTheTypeAgnosticOne()
    {
        // Arrange
        var service = new DefaultInteractionEventUpcastService(
            [
                new AppendStep(1, null, "universal"),
                new AppendStep(1, PresenceChanged, "specific"),
            ],
            2);

        var specific = new InteractionEvent
        {
            ItemId = "event-1",
            EventType = PresenceChanged,
            SchemaVersion = 1,
            Data = """{"steps":""}""",
        };

        var other = new InteractionEvent
        {
            ItemId = "event-2",
            EventType = OfferDeclined,
            SchemaVersion = 1,
            Data = """{"steps":""}""",
        };

        // Act
        service.Upcast(specific);
        service.Upcast(other);

        // Assert
        Assert.Equal("specific", JsonNode.Parse(specific.Data)["steps"].GetValue<string>());
        Assert.Equal("universal", JsonNode.Parse(other.Data)["steps"].GetValue<string>());
    }

    [Fact]
    public void Constructor_WhenTwoUpcastersOwnTheSameStep_Fails()
    {
        // Arrange
        // Choosing either one would make the converted payload depend on registration order, which is not a
        // property anybody reviews.
        var upcasters = new IInteractionEventUpcaster[]
        {
            new AppendStep(1, PresenceChanged, "left"),
            new AppendStep(1, PresenceChanged, "right"),
        };

        // Act
        var exception = Assert.Throws<InteractionEventUpcastException>(
            () => new DefaultInteractionEventUpcastService(upcasters, 2));

        // Assert
        Assert.Contains("Exactly one upcaster may own a version step", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Upcast_WhenTheEventCarriesNoPayload_StillAdvancesTheVersion()
    {
        // Arrange
        var service = new DefaultInteractionEventUpcastService([new AppendStep(1, PresenceChanged, "first")], 2);

        var interactionEvent = new InteractionEvent
        {
            ItemId = "event-1",
            EventType = PresenceChanged,
            SchemaVersion = 1,
            Data = null,
        };

        // Act
        service.Upcast(interactionEvent);

        // Assert
        Assert.Null(interactionEvent.Data);
        Assert.Equal(2, interactionEvent.SchemaVersion);
    }

    [Fact]
    public void Upcast_ConvertsAPayloadTodaysTypeWouldHaveSilentlyMisread()
    {
        // Arrange
        // The end-to-end shape of the failure: a property was renamed between versions. Without conversion the
        // deserialization succeeds and the renamed value arrives as null.
        var service = new DefaultInteractionEventUpcastService([new RenameStep(1, PresenceChanged)], 2);

        var interactionEvent = new InteractionEvent
        {
            ItemId = "event-1",
            EventType = PresenceChanged,
            SchemaVersion = 1,
            Data = """{"Reason":"break"}""",
        };

        var beforeConversion = JsonSerializer.Deserialize<PresencePayload>(interactionEvent.Data);

        // Act
        service.Upcast(interactionEvent);

        // Assert
        Assert.Null(beforeConversion.PresenceReason);
        Assert.Equal("break", interactionEvent.GetData<PresencePayload>().PresenceReason);
    }

    private sealed class PresencePayload
    {
        public string PresenceReason { get; set; }
    }

    private sealed class RenameStep : IInteractionEventUpcaster
    {
        public RenameStep(int fromVersion, string eventType)
        {
            FromVersion = fromVersion;
            EventType = eventType;
        }

        public string EventType { get; }

        public int FromVersion { get; }

        public JsonNode Upcast(JsonNode payload)
        {
            if (payload is not JsonObject json)
            {
                return payload;
            }

            if (json.Remove("Reason", out var reason))
            {
                json["PresenceReason"] = reason;
            }

            return json;
        }
    }

    private sealed class AppendStep : IInteractionEventUpcaster
    {
        private readonly string _marker;

        public AppendStep(int fromVersion, string eventType, string marker)
        {
            FromVersion = fromVersion;
            EventType = eventType;
            _marker = marker;
        }

        public string EventType { get; }

        public int FromVersion { get; }

        public JsonNode Upcast(JsonNode payload)
        {
            if (payload is not JsonObject json)
            {
                return payload;
            }

            var existing = json["steps"]?.GetValue<string>() ?? string.Empty;

            json["steps"] = string.IsNullOrEmpty(existing)
                ? _marker
                : $"{existing}|{_marker}";

            return json;
        }
    }
}
