using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Dialpad.Services;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Telephony.ProviderContracts;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.Dialpad;

/// <summary>
/// Pins the Dialpad webhook normalizer to recorded provider deliveries. Dialpad does not publish a machine-readable
/// schema that can be vendored the way the Asterisk project publishes its REST Interface declarations, so this contract
/// is payload-bound: every provider token the normalizer interprets must carry a recorded expectation, and every
/// recorded delivery must replay through the production ingress, deserializer, and normalizer to the recorded outcome.
/// </summary>
public sealed class DialpadWebhookContractTests
{
    private const string CassetteRelativePath = "tests/CrestApps.OrchardCore.Tests/Telephony/Cassettes/Dialpad";
    private const string NormalizerSourcePath = "src/Modules/CrestApps.OrchardCore.Dialpad/Services/DialpadWebhookService.cs";

    private static readonly DateTime _fallbackNow = new(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Regex _tokenPattern = new(
        "\"(?<token>[a-z_]*)\"",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    [Fact]
    public void CallStateTable_NamesExactlyTheTokensTheNormalizerInterprets()
    {
        // Arrange
        var recorded = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var entry in LoadStates().RootElement.GetProperty("callStates").EnumerateArray())
        {
            recorded.Add(entry.GetProperty("token").GetString());
        }

        // Act
        var interpreted = ExtractSwitchTokens("private static bool TryMapState", "(VoiceCallState)(-1)");

        // Assert
        Assert.NotEmpty(interpreted);
        Assert.Equal(interpreted, recorded);
    }

    [Fact]
    public void RecordingStateTable_NamesExactlyTheTokensTheNormalizerInterprets()
    {
        // Arrange
        var recorded = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var entry in LoadStates().RootElement.GetProperty("recordingStates").EnumerateArray())
        {
            recorded.Add(entry.GetProperty("token").GetString());
        }

        // Act
        var interpreted = ExtractSwitchTokens("private static bool TryMapRecordingState", "(RecordingState)(-1)");

        // Assert
        Assert.NotEmpty(interpreted);
        Assert.Equal(interpreted, recorded);
    }

    [Fact]
    public void AnswerClassificationTable_NamesExactlyTheTokensTheNormalizerInterprets()
    {
        // Arrange
        var recorded = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var entry in LoadStates().RootElement.GetProperty("callStates").EnumerateArray())
        {
            if (entry.TryGetProperty("answerClassification", out _))
            {
                recorded.Add(entry.GetProperty("token").GetString());
            }
        }

        // Act
        var interpreted = ExtractSwitchTokens("private static bool TryMapAnswerClassification", "(AnswerClassification)(-1)");

        // Assert
        Assert.NotEmpty(interpreted);
        Assert.Equal(interpreted, recorded);
    }

    [Fact]
    public async Task EveryRecordedCallStateToken_NormalizesToTheRecordedOutcome()
    {
        // Arrange
        var states = LoadStates();
        var normalized = 0;

        // Act & Assert
        foreach (var entry in states.RootElement.GetProperty("callStates").EnumerateArray())
        {
            var token = entry.GetProperty("token").GetString();
            var captured = await ProcessAsync(
                $$"""{"call_id":"3456789012345678","state":"{{token}}","direction":"outbound","external_number":"+15125550188","event_timestamp":1699887601456}""",
                providerHandled: true);

            Assert.Equal(DialpadWebhookResult.Updated, captured.Result);
            Assert.Equal(
                Enum.Parse<VoiceCallState>(entry.GetProperty("contactCenterCallState").GetString()),
                captured.ProviderEvent.State);

            if (entry.TryGetProperty("answerClassification", out var classification))
            {
                Assert.Equal(Enum.Parse<AnswerClassification>(classification.GetString()), captured.ProviderEvent.AnswerClassification);
            }
            else
            {
                Assert.Null(captured.ProviderEvent.AnswerClassification);
            }

            normalized++;
        }

        Assert.True(normalized >= 30, $"Only {normalized} Dialpad call state tokens were replayed, which is too few to be a meaningful contract.");
    }

    [Fact]
    public async Task EveryRecordedRecordingStateToken_NormalizesToTheRecordedOutcome()
    {
        // Arrange
        var states = LoadStates();

        // Act & Assert
        foreach (var entry in states.RootElement.GetProperty("recordingStates").EnumerateArray())
        {
            var token = entry.GetProperty("token").GetString();
            var captured = await ProcessAsync(
                $$"""{"call_id":"3456789012345678","state":"connected","direction":"outbound","recording_state":"{{token}}"}""",
                providerHandled: true);

            Assert.Equal(
                Enum.Parse<RecordingState>(entry.GetProperty("recordingState").GetString()),
                captured.ProviderEvent.RecordingState);
        }
    }

    [Fact]
    public async Task RecordedUnmappedTokens_AreIgnoredRatherThanGuessedAt()
    {
        // Arrange
        var states = LoadStates();

        // Act & Assert
        foreach (var token in states.RootElement.GetProperty("unmappedTokens").EnumerateArray())
        {
            var captured = await ProcessAsync(
                $$"""{"call_id":"3456789012345678","state":"{{token.GetString()}}","direction":"inbound","external_number":"+15125550188"}""",
                providerHandled: false);

            Assert.Equal(DialpadWebhookResult.Ignored, captured.Result);
            Assert.Null(captured.ProviderEvent);
        }
    }

    [Fact]
    public async Task RecordedScenarios_ReplayThroughTheProductionNormalizerToTheRecordedOutcome()
    {
        // Arrange
        var scenarios = LoadScenarios();
        var replayed = 0;

        // Act & Assert
        foreach (var (name, content) in scenarios)
        {
            using var document = JsonDocument.Parse(content);
            var expected = document.RootElement.GetProperty("expected");
            var providerHandled = expected.TryGetProperty("providerHandled", out var handled) && handled.GetBoolean();
            var captured = await ProcessAsync(document.RootElement.GetProperty("payload").GetRawText(), providerHandled);

            Assert.Equal(
                Enum.Parse<DialpadWebhookResult>(expected.GetProperty("result").GetString()),
                captured.Result);
            AssertExpectation(name, expected, captured);

            replayed++;
        }

        Assert.True(replayed >= 8, $"Only {replayed} Dialpad scenarios were replayed, which is too few to be a meaningful contract.");
    }

    [Fact]
    public async Task RecordedScenarios_SurviveTheSignedWebhookIngressPath()
    {
        // Arrange
        const string SigningSecret = "dialpad-contract-signing-secret";
        var scenarios = LoadScenarios();

        // Act & Assert
        foreach (var (name, content) in scenarios)
        {
            using var document = JsonDocument.Parse(content);
            var payloadJson = document.RootElement.GetProperty("payload").GetRawText();
            var signedBody = CreateSignedJwt(payloadJson, SigningSecret);

            Assert.True(
                DialpadJwtValidator.TryValidateAndExtract(signedBody, SigningSecret, out var extracted),
                $"Scenario '{name}' was rejected by the signed webhook ingress path.");

            var tampered = CreateSignedJwt(payloadJson, "a-different-secret");

            Assert.False(
                DialpadJwtValidator.TryValidateAndExtract(tampered, SigningSecret, out _),
                $"Scenario '{name}' was accepted with a signature the configured secret does not produce.");

            var callEvent = JsonSerializer.Deserialize<DialpadCallEvent>(extracted, DialpadJsonSerializerOptions.Default);
            var expectedEvent = JsonSerializer.Deserialize<DialpadCallEvent>(payloadJson, DialpadJsonSerializerOptions.Default);

            Assert.Equal(expectedEvent.CallId, callEvent.CallId);
            Assert.Equal(expectedEvent.State, callEvent.State);
        }
    }

    [Fact]
    public void RecordedScenarios_OnlyUseTheSnakeCaseFieldNamesTheProductionDeserializerBinds()
    {
        // Arrange
        var scenarios = LoadScenarios();
        var bindable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in typeof(DialpadCallEvent).GetProperties())
        {
            bindable.Add(JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name));
        }

        // Act & Assert
        foreach (var (name, content) in scenarios)
        {
            using var document = JsonDocument.Parse(content);

            foreach (var field in document.RootElement.GetProperty("payload").EnumerateObject())
            {
                Assert.True(
                    bindable.Contains(field.Name),
                    $"Scenario '{name}' records the field '{field.Name}', which the production deserializer never binds.");
            }
        }
    }

    private static void AssertExpectation(string name, JsonElement expected, CapturedDelivery captured)
    {
        foreach (var property in expected.EnumerateObject())
        {
            switch (property.Name)
            {
                case "result":
                case "providerHandled":
                    break;
                case "state":
                    Assert.Equal(Enum.Parse<VoiceCallState>(property.Value.GetString()), captured.ProviderEvent.State);

                    break;
                case "fromAddress":
                    Assert.Equal(property.Value.GetString(), captured.ProviderEvent.FromAddress);

                    break;
                case "toAddress":
                    Assert.Equal(property.Value.GetString(), captured.ProviderEvent.ToAddress);

                    break;
                case "isMuted":
                    Assert.Equal(property.Value.GetBoolean(), captured.ProviderEvent.IsMuted);

                    break;
                case "isConference":
                    Assert.Equal(property.Value.GetBoolean(), captured.ProviderEvent.IsConference);

                    break;
                case "participantCount":
                    Assert.Equal(property.Value.GetInt32(), captured.ProviderEvent.ParticipantCount);

                    break;
                case "recordingState":
                    Assert.Equal(Enum.Parse<RecordingState>(property.Value.GetString()), captured.ProviderEvent.RecordingState);

                    break;
                case "recordingReference":
                    Assert.Equal(property.Value.GetString(), captured.ProviderEvent.RecordingReference);

                    break;
                case "answerClassification":
                    Assert.Equal(Enum.Parse<AnswerClassification>(property.Value.GetString()), captured.ProviderEvent.AnswerClassification);

                    break;
                case "occurredUtc":
                    Assert.Equal(
                        DateTime.Parse(property.Value.GetString(), null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal),
                        captured.ProviderEvent.OccurredUtc);

                    break;
                case "callerName":
                    Assert.Equal(property.Value.GetString(), captured.InboundEvent.CallerName);

                    break;
                default:
                    Assert.Fail($"Scenario '{name}' declares the unsupported expectation '{property.Name}'.");

                    break;
            }
        }
    }

    private static async Task<CapturedDelivery> ProcessAsync(string payloadJson, bool providerHandled)
    {
        var callEvent = JsonSerializer.Deserialize<DialpadCallEvent>(payloadJson, DialpadJsonSerializerOptions.Default);
        ProviderVoiceEvent providerEvent = null;
        InboundVoiceEvent inboundEvent = null;

        var ingestor = new Mock<INormalizedVoiceEventIngestor>();
        ingestor
            .Setup(sink => sink.IngestAsync(It.IsAny<ProviderVoiceEvent>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderVoiceEvent, CancellationToken>((captured, _) => providerEvent = captured)
            .ReturnsAsync(providerHandled);

        var inboundSink = new Mock<IInboundVoiceEventSink>();
        inboundSink
            .Setup(sink => sink.RouteAsync(It.IsAny<InboundVoiceEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InboundVoiceEvent, CancellationToken>((captured, _) => inboundEvent = captured)
            .ReturnsAsync(new InboundVoiceRouteOutcome());

        var clock = new Mock<IClock>();
        clock.SetupGet(instance => instance.UtcNow).Returns(_fallbackNow);

        var service = new DialpadWebhookService(
            ingestor.Object,
            new ContactCenterDialpadInboundCallRouter(inboundSink.Object),
            clock.Object);
        var result = await service.ProcessAsync(callEvent, TestContext.Current.CancellationToken);

        return new CapturedDelivery(result, providerEvent, inboundEvent);
    }

    private static string CreateSignedJwt(string payloadJson, string secret)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"HS256","typ":"JWT"}"""));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        var signingInput = Encoding.UTF8.GetBytes($"{header}.{payload}");
        var signature = Base64UrlEncode(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signingInput));

        return $"{header}.{payload}.{signature}";
    }

    private static string Base64UrlEncode(byte[] value)
    {
        return Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static SortedSet<string> ExtractSwitchTokens(string methodSignature, string sentinel)
    {
        var source = AsteriskContractCassettes.ReadRepositoryFile(NormalizerSourcePath);
        var start = source.IndexOf(methodSignature, StringComparison.Ordinal);

        Assert.True(start >= 0, $"The normalizer no longer declares '{methodSignature}'.");

        var end = source.IndexOf(sentinel, start, StringComparison.Ordinal);

        Assert.True(end > start, $"The normalizer no longer terminates '{methodSignature}' with '{sentinel}'.");

        var body = source.Substring(start, end - start);
        var tokens = new SortedSet<string>(StringComparer.Ordinal);

        foreach (System.Text.RegularExpressions.Match match in _tokenPattern.Matches(body))
        {
            var token = match.Groups["token"].Value;

            if (!string.IsNullOrEmpty(token))
            {
                tokens.Add(token);
            }
        }

        return tokens;
    }

    private static JsonDocument LoadStates()
    {
        return JsonDocument.Parse(AsteriskContractCassettes.ReadRepositoryFile($"{CassetteRelativePath}/states.json"));
    }

    private static Dictionary<string, string> LoadScenarios()
    {
        var scenarios = new Dictionary<string, string>(StringComparer.Ordinal);
        var scenarioDirectory = ResolveScenarioDirectory();

        foreach (var file in Directory.GetFiles(scenarioDirectory, "*.json"))
        {
            scenarios[Path.GetFileNameWithoutExtension(file)] = File.ReadAllText(file);
        }

        return scenarios;
    }

    private static string ResolveScenarioDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        var repositoryRoot = directory?.FullName ??
            throw new InvalidOperationException("The repository root could not be located.");

        return Path.Combine(
            repositoryRoot,
            $"{CassetteRelativePath}/scenarios".Replace('/', Path.DirectorySeparatorChar));
    }

    private sealed record CapturedDelivery(
        DialpadWebhookResult Result,
        ProviderVoiceEvent ProviderEvent,
        InboundVoiceEvent InboundEvent);
}
