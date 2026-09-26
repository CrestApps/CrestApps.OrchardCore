using System.Text.Json;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Telephony.ProviderContracts;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Pins the Asterisk realtime voice mapper to the Asterisk REST Interface event contract published for the pinned
/// Asterisk release. Every payload the mapper is asked to interpret is proven to be expressible by the provider, every
/// field the mapper depends on is proven to exist in the provider's own declarations, and every field the mapper only
/// tolerates is proven to be absent from them, so an Asterisk upgrade that moves or renames a field breaks the build
/// instead of silently degrading call control in production.
/// </summary>
public sealed class AsteriskAriEventContractTests
{
    private const string ProviderName = "asterisk";
    private const string MapperSourcePath = "src/Modules/CrestApps.OrchardCore.Asterisk/Services/AsteriskRealtimeVoiceEventMapper.cs";

    private static readonly Regex _handledEventTypePattern = new(
        @"string\.Equals\(\s*eventType\s*,\s*""(?<eventType>[A-Za-z]+)""",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    [Fact]
    public void EventContract_CoversExactlyTheEventTypesTheMapperInterprets()
    {
        // Arrange
        var contract = LoadContract();
        var source = AsteriskContractCassettes.ReadRepositoryFile(MapperSourcePath);

        // Act
        var interpreted = new SortedSet<string>(StringComparer.Ordinal);

        foreach (Match match in _handledEventTypePattern.Matches(source))
        {
            interpreted.Add(match.Groups["eventType"].Value);
        }

        var covered = new SortedSet<string>(ReadContractEventTypes(contract), StringComparer.Ordinal);

        // Assert
        Assert.NotEmpty(interpreted);
        Assert.Equal(interpreted, covered);
    }

    [Fact]
    public void EventContract_OnlyDependsOnFieldsAsteriskActuallyDeclares()
    {
        // Arrange
        var cassettes = AsteriskContractCassettes.Load();
        var contract = LoadContract();

        // Act & Assert
        foreach (var declaredEvent in contract.RootElement.GetProperty("events").EnumerateArray())
        {
            var eventType = declaredEvent.GetProperty("ariEventType").GetString();

            Assert.True(
                cassettes.Specification.Models.ContainsKey(eventType),
                $"Asterisk {cassettes.Version} does not declare the '{eventType}' event the mapper interprets.");

            foreach (var path in declaredEvent.GetProperty("specificationBackedPaths").EnumerateArray())
            {
                var propertyPath = path.GetString();

                Assert.True(
                    cassettes.Specification.DeclaresPropertyPath(eventType, propertyPath),
                    $"Asterisk {cassettes.Version} does not declare '{propertyPath}' on the '{eventType}' event, so the mapper depends on a field the provider does not publish.");
            }
        }
    }

    [Fact]
    public void EventContract_KeepsToleratedCompatibilityFallbacksInertAgainstAConformingRelease()
    {
        // Arrange
        var cassettes = AsteriskContractCassettes.Load();
        var contract = LoadContract();
        var eventTypes = ReadContractEventTypes(contract);

        // Act & Assert
        foreach (var tolerated in contract.RootElement.GetProperty("toleratedNonSpecificationPaths").EnumerateArray())
        {
            var path = tolerated.GetProperty("path").GetString();

            Assert.False(
                string.IsNullOrWhiteSpace(tolerated.GetProperty("justification").GetString()),
                $"The tolerated path '{path}' must justify why the mapper reads a field Asterisk does not publish.");

            foreach (var eventType in eventTypes)
            {
                Assert.False(
                    cassettes.Specification.DeclaresPropertyPath(eventType, path),
                    $"Asterisk {cassettes.Version} now declares '{path}' on '{eventType}'. Promote it to a specification-backed path instead of leaving it as a tolerated fallback.");
            }
        }
    }

    [Fact]
    public void EventCassettes_ContainOnlyPayloadsAConformingAsteriskReleaseCouldEmit()
    {
        // Arrange
        var cassettes = AsteriskContractCassettes.Load();

        // Act & Assert
        foreach (var (name, content) in cassettes.ReadCassettes("events"))
        {
            using var document = JsonDocument.Parse(content);
            var eventType = document.RootElement.GetProperty("ariEventType").GetString();
            var payload = document.RootElement.GetProperty("payload");
            var undeclared = cassettes.Specification.FindUndeclaredPaths(eventType, payload);

            Assert.True(
                undeclared.Count == 0,
                $"Cassette '{name}' contains fields Asterisk {cassettes.Version} does not declare on '{eventType}': {string.Join(", ", undeclared)}.");
            Assert.Equal(eventType, payload.GetProperty("type").GetString());
        }
    }

    [Fact]
    public void EventCassettes_CoverEveryEventTypeTheMapperInterprets()
    {
        // Arrange
        var cassettes = AsteriskContractCassettes.Load();
        var contract = LoadContract();

        // Act
        var recorded = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var (_, content) in cassettes.ReadCassettes("events"))
        {
            using var document = JsonDocument.Parse(content);
            recorded.Add(document.RootElement.GetProperty("ariEventType").GetString());
        }

        // Assert
        Assert.Equal(new SortedSet<string>(ReadContractEventTypes(contract), StringComparer.Ordinal), recorded);
    }

    [Fact]
    public void EventCassettes_ReplayThroughTheProductionMapperToTheRecordedOutcome()
    {
        // Arrange
        var cassettes = AsteriskContractCassettes.Load();
        var replayed = 0;

        // Act & Assert
        foreach (var (name, content) in cassettes.ReadCassettes("events"))
        {
            using var document = JsonDocument.Parse(content);
            var payload = document.RootElement.GetProperty("payload").GetRawText();
            var expected = document.RootElement.GetProperty("expected");
            var mapped = AsteriskRealtimeVoiceEventMapper.TryMap(ProviderName, payload, out var voiceEvent);

            if (expected.TryGetProperty("unmapped", out var unmapped) && unmapped.GetBoolean())
            {
                Assert.False(mapped, $"Cassette '{name}' is recorded as unmappable but the mapper accepted it.");

                replayed++;

                continue;
            }

            Assert.True(mapped, $"Cassette '{name}' is recorded as mappable but the mapper rejected it.");
            AssertExpectation(name, expected, voiceEvent);

            replayed++;
        }

        Assert.True(replayed >= 15, $"Only {replayed} Asterisk event cassettes were replayed, which is too few to be a meaningful contract.");
    }

    [Fact]
    public void EventCassettes_ProduceAStableIdempotencyKeyPerRecordedPayload()
    {
        // Arrange
        var cassettes = AsteriskContractCassettes.Load();
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);

        // Act
        foreach (var (name, content) in cassettes.ReadCassettes("events"))
        {
            using var document = JsonDocument.Parse(content);
            var payload = document.RootElement.GetProperty("payload").GetRawText();

            if (!AsteriskRealtimeVoiceEventMapper.TryMap(ProviderName, payload, out var voiceEvent))
            {
                continue;
            }

            Assert.True(AsteriskRealtimeVoiceEventMapper.TryMap(ProviderName, payload, out var replayedEvent));
            Assert.Equal(voiceEvent.IdempotencyKey, replayedEvent.IdempotencyKey);

            keys[name] = voiceEvent.IdempotencyKey;
        }

        // Assert
        Assert.Equal(keys.Count, keys.Values.Distinct(StringComparer.Ordinal).Count());
    }

    private static void AssertExpectation(string name, JsonElement expected, AsteriskRealtimeVoiceEvent voiceEvent)
    {
        foreach (var property in expected.EnumerateObject())
        {
            switch (property.Name)
            {
                case "eventType":
                    Assert.Equal(property.Value.GetString(), voiceEvent.EventType);

                    break;
                case "state":
                    Assert.Equal(Enum.Parse<CallState>(property.Value.GetString()), voiceEvent.State);

                    break;
                case "isInbound":
                    Assert.Equal(property.Value.GetBoolean(), voiceEvent.IsInbound);

                    break;
                case "isOwnedOrigination":
                    Assert.Equal(property.Value.GetBoolean(), voiceEvent.IsOwnedOrigination);

                    break;
                case "isOnHold":
                    Assert.Equal(property.Value.GetBoolean(), voiceEvent.IsOnHold);

                    break;
                case "isMuted":
                    Assert.Equal(property.Value.GetBoolean(), voiceEvent.IsMuted);

                    break;
                case "isConference":
                    Assert.Equal(property.Value.GetBoolean(), voiceEvent.IsConference);

                    break;
                case "participantCount":
                    Assert.Equal(property.Value.GetInt32(), voiceEvent.ParticipantCount);

                    break;
                case "callerNumber":
                    Assert.Equal(property.Value.GetString(), voiceEvent.CallerNumber);

                    break;
                case "dialedNumber":
                    Assert.Equal(property.Value.GetString(), voiceEvent.DialedNumber);

                    break;
                case "interactionCorrelationId":
                    Assert.Equal(property.Value.GetString(), voiceEvent.InteractionCorrelationId);

                    break;
                default:
                    Assert.Fail($"Cassette '{name}' declares the unsupported expectation '{property.Name}'.");

                    break;
            }
        }
    }

    private static List<string> ReadContractEventTypes(JsonDocument contract)
    {
        var eventTypes = new List<string>();

        foreach (var declaredEvent in contract.RootElement.GetProperty("events").EnumerateArray())
        {
            eventTypes.Add(declaredEvent.GetProperty("ariEventType").GetString());
        }

        return eventTypes;
    }

    private static JsonDocument LoadContract()
    {
        var cassettes = AsteriskContractCassettes.Load();

        return JsonDocument.Parse(File.ReadAllText(Path.Combine(cassettes.DirectoryPath, "contract.json")));
    }
}
