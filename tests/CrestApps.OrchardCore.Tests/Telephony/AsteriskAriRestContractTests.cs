using System.Net;
using System.Text.Json;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Tests.Telephony.ProviderContracts;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Replays recorded Asterisk REST Interface responses through the production <see cref="AsteriskAriClient"/> and proves
/// every request the client issues is an operation the Asterisk project declares for the pinned release, with only query
/// parameters that release accepts. A path typo, an invented query parameter, or an Asterisk upgrade that retires an
/// operation fails here instead of failing the first live call.
/// </summary>
public sealed class AsteriskAriRestContractTests
{
    [Fact]
    public async Task EveryClientOperation_IssuesRequestsTheAsteriskReleaseDeclares()
    {
        // Arrange
        var cassettes = AsteriskContractCassettes.Load();
        var harness = new AriRestContractHarness(cassettes);

        // Act
        await harness.ExerciseEveryOperationAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(harness.IssuedRequests);

        foreach (var issued in harness.IssuedRequests)
        {
            Assert.True(
                cassettes.Specification.TryFindOperation(issued.HttpMethod, issued.Path, out var operation),
                $"Asterisk {cassettes.Version} does not declare '{issued.HttpMethod} {issued.Path}', which {issued.Operation} issues.");

            foreach (var queryParameterName in issued.QueryParameterNames)
            {
                Assert.True(
                    operation.QueryParameterNames.Contains(queryParameterName),
                    $"Asterisk {cassettes.Version} does not accept the '{queryParameterName}' query parameter on '{operation.Description}', which {issued.Operation} sends.");
            }
        }
    }

    [Fact]
    public async Task EveryClientOperation_IsCoveredSoNewOperationsCannotSkipTheContract()
    {
        // Arrange
        var cassettes = AsteriskContractCassettes.Load();
        var harness = new AriRestContractHarness(cassettes);

        // Act
        await harness.ExerciseEveryOperationAsync(TestContext.Current.CancellationToken);

        var declaredOperations = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var method in typeof(IAsteriskAriClient).GetMethods())
        {
            declaredOperations.Add(method.Name);
        }

        // Assert
        Assert.Equal(declaredOperations, new SortedSet<string>(harness.ExercisedOperations, StringComparer.Ordinal));
    }

    [Fact]
    public async Task RecordedResponses_AreParsedIntoTheDomainModelTheCallersRelyOn()
    {
        // Arrange
        var cassettes = AsteriskContractCassettes.Load();
        var harness = new AriRestContractHarness(cassettes);
        var client = harness.CreateClient();

        // Act
        var channel = await client.OriginateAsync(AriRestContractHarness.CreateOriginateRequest(), TestContext.Current.CancellationToken);
        var bridge = await client.CreateBridgeAsync("conf-01HTQ0", "mixing", TestContext.Current.CancellationToken);
        var liveRecording = await client.StartBridgeRecordingAsync("conf-01HTQ0", "rec-01HTQ0", "wav", TestContext.Current.CancellationToken);
        var storedRecording = await client.StopBridgeRecordingAsync("rec-01HTQ0", TestContext.Current.CancellationToken);
        var snooped = await client.SnoopChannelAsync("1699887600.42", "both", "none", "snoop-01HTQ0", TestContext.Current.CancellationToken);
        var exists = await client.ChannelExistsAsync("1699887600.42", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("1699887600.42", channel.Id);
        Assert.Equal("Up", channel.State);
        Assert.Equal("conf-01HTQ0", bridge.Id);
        Assert.Equal("mixing", bridge.BridgeType);
        Assert.Equal(2, bridge.Channels.Count);
        Assert.Equal("rec-01HTQ0", liveRecording.Name);
        Assert.Equal("wav", liveRecording.Format);
        Assert.Equal("wav", storedRecording.Format);
        Assert.Equal("snoop-01HTQ0", snooped.Id);
        Assert.True(exists);
    }

    [Fact]
    public async Task StopRecording_ReportsTheDurationAsteriskOnlyPublishesWhileTheRecordingIsLive()
    {
        // Arrange
        var cassettes = AsteriskContractCassettes.Load();
        var harness = new AriRestContractHarness(cassettes);
        var client = harness.CreateClient();

        // Act
        var storedRecording = await client.StopBridgeRecordingAsync("rec-01HTQ0", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(cassettes.Specification.GetDeclaredPropertyType("StoredRecording", "duration"));
        Assert.NotNull(cassettes.Specification.GetDeclaredPropertyType("LiveRecording", "duration"));
        Assert.Equal(42, storedRecording.Duration);
    }

    [Fact]
    public void RecordedResponses_OnlyContainBodiesAConformingAsteriskReleaseCouldSend()
    {
        // Arrange
        var cassettes = AsteriskContractCassettes.Load();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(cassettes.DirectoryPath, "rest", "responses.json")));

        // Act & Assert
        foreach (var recorded in document.RootElement.GetProperty("responses").EnumerateArray())
        {
            var httpMethod = recorded.GetProperty("httpMethod").GetString();
            var pathTemplate = recorded.GetProperty("pathTemplate").GetString();

            Assert.True(
                cassettes.Specification.TryFindOperation(httpMethod, pathTemplate, out _),
                $"The recorded response for '{httpMethod} {pathTemplate}' is not an operation Asterisk {cassettes.Version} declares.");

            if (!recorded.TryGetProperty("model", out var model) || !recorded.TryGetProperty("body", out var body))
            {
                continue;
            }

            var undeclared = cassettes.Specification.FindUndeclaredPaths(model.GetString(), body);

            Assert.True(
                undeclared.Count == 0,
                $"The recorded response for '{httpMethod} {pathTemplate}' contains fields Asterisk {cassettes.Version} does not declare on '{model.GetString()}': {string.Join(", ", undeclared)}.");
        }
    }

    [Fact]
    public async Task RecordedResponses_CoverEveryOperationTheClientIssues()
    {
        // Arrange
        var cassettes = AsteriskContractCassettes.Load();
        var harness = new AriRestContractHarness(cassettes);

        // Act
        await harness.ExerciseEveryOperationAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(harness.UnrecordedRequests);
        Assert.DoesNotContain(HttpStatusCode.NotImplemented, harness.IssuedStatusCodes);
    }
}
