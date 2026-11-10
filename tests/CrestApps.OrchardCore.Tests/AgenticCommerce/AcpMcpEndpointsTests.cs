using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce;
using CrestApps.OrchardCore.AgenticCommerce.Acp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// ACP's MCP binding takes <c>{ meta, id?, payload? }</c> for each checkout tool, answers with the REST body in
/// <c>structuredContent</c>, maps every ACP error to <c>-32000</c> with the error in <c>data</c>, keeps <c>-32602</c>
/// for malformed arguments, and follows the REST binding's idempotency rules with the key in <c>meta</c>.
/// </summary>
public sealed class AcpMcpEndpointsTests
{
    [Fact]
    public void TryReadCall_ReadsMetaIdAndPayload()
    {
        // Arrange
        var parameters = Params(AcpMcpEndpoints.Tools.Complete, new JsonObject
        {
            ["meta"] = new JsonObject
            {
                ["api_version"] = AgenticCommerceConstants.Versions.Acp20260417,
                ["idempotency_key"] = "key-1",
                ["request_id"] = "req-1",
                ["signature"] = "sig",
                ["timestamp"] = "2026-11-10T12:00:00Z",
                ["user_agent"] = "Agent/1.0",
            },
            ["id"] = "cs_1",
            ["payload"] = new JsonObject { ["payment_data"] = new JsonObject { ["token"] = "tok" } },
        });

        // Act
        var read = AcpMcpEndpoints.TryReadCall(parameters, out var call, out var error);

        // Assert
        Assert.True(read, error);
        Assert.Equal(AcpMcpEndpoints.Tools.Complete, call.Tool);
        Assert.Equal("cs_1", call.Id);
        Assert.Equal(AgenticCommerceConstants.Versions.Acp20260417, call.ApiVersion);
        Assert.Equal("key-1", call.IdempotencyKey);
        Assert.Equal("req-1", call.RequestId);
        Assert.Equal("sig", call.Signature);
        Assert.Equal("2026-11-10T12:00:00Z", call.Timestamp);
        Assert.Equal("tok", call.Payload["payment_data"]!["token"]!.GetValue<string>());
        Assert.True(call.IsMutating);
    }

    [Fact]
    public void TryReadCall_CreateNeedsNoId_AndGetNeedsNoPayload()
    {
        // Arrange
        var create = Params(AcpMcpEndpoints.Tools.Create, Arguments(payload: new JsonObject { ["line_items"] = new JsonArray() }));
        var get = Params(AcpMcpEndpoints.Tools.Get, Arguments(id: "cs_1"));

        // Act
        var createRead = AcpMcpEndpoints.TryReadCall(create, out var createCall, out _);
        var getRead = AcpMcpEndpoints.TryReadCall(get, out var getCall, out _);

        // Assert
        Assert.True(createRead);
        Assert.Null(createCall.Id);
        Assert.True(getRead);
        Assert.Null(getCall.Payload);
        Assert.False(getCall.IsMutating);
    }

    [Fact]
    public void TryReadCall_CancelTakesAnOptionalPayload()
    {
        // Act
        var read = AcpMcpEndpoints.TryReadCall(Params(AcpMcpEndpoints.Tools.Cancel, Arguments(id: "cs_1")), out var call, out _);

        // Assert
        Assert.True(read);
        Assert.True(call.IsMutating);
        Assert.Null(call.Payload);
    }

    [Theory]
    [InlineData("no name")]
    [InlineData("unknown tool")]
    [InlineData("no arguments")]
    [InlineData("no meta")]
    [InlineData("no api_version")]
    [InlineData("get without id")]
    [InlineData("update without id")]
    [InlineData("create without payload")]
    [InlineData("complete without payload")]
    [InlineData("payload not an object")]
    public void TryReadCall_MalformedArguments_AreRefused(string reason)
    {
        // Arrange
        var parameters = reason switch
        {
            "no name" => new JsonObject { ["arguments"] = Arguments(payload: []) },
            "unknown tool" => Params("delete_checkout_session", Arguments(id: "cs_1")),
            "no arguments" => new JsonObject { ["name"] = AcpMcpEndpoints.Tools.Get },
            "no meta" => Params(AcpMcpEndpoints.Tools.Get, new JsonObject { ["id"] = "cs_1" }),
            "no api_version" => Params(AcpMcpEndpoints.Tools.Get, new JsonObject { ["meta"] = new JsonObject(), ["id"] = "cs_1" }),
            "get without id" => Params(AcpMcpEndpoints.Tools.Get, Arguments()),
            "update without id" => Params(AcpMcpEndpoints.Tools.Update, Arguments(payload: [])),
            "create without payload" => Params(AcpMcpEndpoints.Tools.Create, Arguments()),
            "complete without payload" => Params(AcpMcpEndpoints.Tools.Complete, Arguments(id: "cs_1")),
            _ => Params(AcpMcpEndpoints.Tools.Update, new JsonObject { ["meta"] = Meta(), ["id"] = "cs_1", ["payload"] = new JsonArray() }),
        };

        // Act
        var read = AcpMcpEndpoints.TryReadCall(parameters, out var call, out var error);

        // Assert
        Assert.False(read, reason);
        Assert.Null(call);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void ToResponse_OnSuccess_PutsTheRestBodyInStructuredContent()
    {
        // Arrange
        var body = new JsonObject { ["id"] = "cs_1", ["status"] = "ready_for_payment" };

        // Act
        var response = AcpMcpEndpoints.ToResponse(JsonValue.Create(7), AcpOperationResult.Success(body, StatusCodes.Status201Created), "req-1", replayed: true);

        // Assert
        Assert.Equal("2.0", response["jsonrpc"]!.GetValue<string>());
        Assert.Equal(7, response["id"]!.GetValue<int>());
        Assert.Null(response["error"]);

        var result = response["result"]!.AsObject();

        Assert.False(result["isError"]!.GetValue<bool>());
        Assert.Equal("cs_1", result["structuredContent"]!["id"]!.GetValue<string>());
        Assert.Contains("ready_for_payment", result["content"]![0]!["text"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("req-1", result["_meta"]!["request_id"]!.GetValue<string>());
        Assert.True(result["_meta"]!["idempotent_replayed"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData(StatusCodes.Status404NotFound, "not_found")]
    [InlineData(StatusCodes.Status401Unauthorized, "invalid_signature")]
    [InlineData(StatusCodes.Status422UnprocessableEntity, "idempotency_conflict")]
    [InlineData(StatusCodes.Status400BadRequest, "idempotency_key_required")]
    public void ToResponse_OnAnAcpError_IsMinus32000WithTheErrorInData(int statusCode, string code)
    {
        // Act
        var response = AcpMcpEndpoints.ToResponse(JsonValue.Create("abc"), AcpOperationResult.Error(statusCode, code, "Something went wrong.", "$.id"));

        // Assert
        Assert.Null(response["result"]);
        Assert.Equal("abc", response["id"]!.GetValue<string>());

        var error = response["error"]!.AsObject();

        Assert.Equal(AcpMcpEndpoints.AcpErrorCode, error["code"]!.GetValue<int>());
        Assert.Equal("Something went wrong.", error["message"]!.GetValue<string>());
        Assert.Equal("invalid_request", error["data"]!["type"]!.GetValue<string>());
        Assert.Equal(code, error["data"]!["code"]!.GetValue<string>());
        Assert.Equal("$.id", error["data"]!["param"]!.GetValue<string>());
    }

    [Fact]
    public void ToResponse_OnAServerError_KeepsTheProcessingErrorType()
    {
        // Act
        var response = AcpMcpEndpoints.ToResponse(JsonValue.Create(1), AcpOperationResult.Error(StatusCodes.Status503ServiceUnavailable, "unavailable", "Try later."));

        // Assert
        Assert.Equal("processing_error", response["error"]!["data"]!["type"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("2025-01-01", "unsupported_api_version")]
    [InlineData("", "missing_api_version")]
    public void CheckVersion_RefusesVersionsTheStoreDoesNotServe(string version, string code)
    {
        // Act
        var error = AcpRequestFilter.CheckVersion(version);
        var response = AcpMcpEndpoints.AcpError(JsonValue.Create(1), error);

        // Assert
        Assert.Equal(code, response["error"]!["data"]!["code"]!.GetValue<string>());
        Assert.NotEmpty(response["error"]!["data"]!["supported_versions"]!.AsArray());
    }

    [Fact]
    public void CheckVersion_AcceptsTheServedVersions()
    {
        // Act and Assert
        Assert.Null(AcpRequestFilter.CheckVersion(AgenticCommerceConstants.Versions.Acp20260417));
        Assert.Null(AcpRequestFilter.CheckVersion(AgenticCommerceConstants.Versions.Acp20260130));
    }

    [Fact]
    public async Task DiscoveryContributor_AddsTheMcpTransportOnce()
    {
        // Arrange
        var document = new JsonObject { ["transports"] = new JsonArray("rest") };
        var contributor = new AcpMcpDiscoveryContributor();

        // Act
        await contributor.ContributeAsync(document, "https://store.example");
        await contributor.ContributeAsync(document, "https://store.example");

        // Assert
        Assert.Equal(new[] { "rest", "mcp" }, document["transports"]!.AsArray().Select(node => node!.GetValue<string>()));
    }

    [Fact]
    public async Task Idempotency_ReplaysTheFirstAnswerForAnEqualPayload()
    {
        // Arrange
        var idempotency = Idempotency();
        var runs = 0;

        Task<AcpOperationResult> RunAsync()
        {
            runs++;

            return Task.FromResult(AcpOperationResult.Success(new JsonObject { ["id"] = "cs_" + runs }, StatusCodes.Status201Created));
        }

        // Act
        var first = await idempotency.RunAsync("p1", AcpMcpEndpoints.Tools.Create, null, "key-1", JsonNode.Parse("{\"a\":1,\"b\":[1,2]}"), RunAsync, TestContext.Current.CancellationToken);
        var second = await idempotency.RunAsync("p1", AcpMcpEndpoints.Tools.Create, null, "key-1", JsonNode.Parse("{ \"b\": [1, 2], \"a\": 1.0 }"), RunAsync, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, runs);
        Assert.False(first.Replayed);
        Assert.True(second.Replayed);
        Assert.Equal(StatusCodes.Status201Created, second.Result.StatusCode);
        Assert.Equal("cs_1", second.Result.Body["id"]!.GetValue<string>());
    }

    [Fact]
    public async Task Idempotency_ADifferentPayload_IsAConflict()
    {
        // Arrange
        var idempotency = Idempotency();

        static Task<AcpOperationResult> RunAsync()
            => Task.FromResult(AcpOperationResult.Success(new JsonObject { ["id"] = "cs_1" }));

        await idempotency.RunAsync("p1", AcpMcpEndpoints.Tools.Update, "cs_1", "key-1", JsonNode.Parse("{\"a\":1}"), RunAsync, TestContext.Current.CancellationToken);

        // Act
        var (result, replayed) = await idempotency.RunAsync("p1", AcpMcpEndpoints.Tools.Update, "cs_1", "key-1", JsonNode.Parse("{\"a\":2}"), RunAsync, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(replayed);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
        Assert.Equal("idempotency_conflict", result.Body["code"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(null, "idempotency_key_required")]
    [InlineData("", "idempotency_key_required")]
    public async Task Idempotency_WithoutAKey_IsRefused(string key, string code)
    {
        // Arrange
        var idempotency = Idempotency();
        var ran = false;

        // Act
        var (result, _) = await idempotency.RunAsync("p1", AcpMcpEndpoints.Tools.Create, null, key, new JsonObject(), () =>
        {
            ran = true;

            return Task.FromResult(AcpOperationResult.Success([]));
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(ran);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal(code, result.Body["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Idempotency_ARetryableAnswer_IsRunAgain()
    {
        // Arrange
        var idempotency = Idempotency();
        var runs = 0;

        Task<AcpOperationResult> RunAsync()
        {
            runs++;

            return Task.FromResult(runs == 1
                ? AcpOperationResult.Error(StatusCodes.Status409Conflict, "conflict", "Busy.", retryAfterSeconds: 1)
                : AcpOperationResult.Success(new JsonObject { ["id"] = "cs_1" }));
        }

        // Act
        await idempotency.RunAsync("p1", AcpMcpEndpoints.Tools.Complete, "cs_1", "key-1", new JsonObject(), RunAsync, TestContext.Current.CancellationToken);
        var (result, replayed) = await idempotency.RunAsync("p1", AcpMcpEndpoints.Tools.Complete, "cs_1", "key-1", new JsonObject(), RunAsync, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, runs);
        Assert.False(replayed);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Idempotency_KeysAreKeptPerPlatformAndCheckout()
    {
        // Arrange
        var idempotency = Idempotency();
        var runs = 0;

        Task<AcpOperationResult> RunAsync()
        {
            runs++;

            return Task.FromResult(AcpOperationResult.Success([]));
        }

        // Act
        await idempotency.RunAsync("p1", AcpMcpEndpoints.Tools.Cancel, "cs_1", "key-1", null, RunAsync, TestContext.Current.CancellationToken);
        await idempotency.RunAsync("p2", AcpMcpEndpoints.Tools.Cancel, "cs_1", "key-1", null, RunAsync, TestContext.Current.CancellationToken);
        await idempotency.RunAsync("p1", AcpMcpEndpoints.Tools.Cancel, "cs_2", "key-1", null, RunAsync, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, runs);
    }

    private static AcpMcpIdempotency Idempotency()
        => new(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            new LocalLock(NullLogger<LocalLock>.Instance),
            new ShellSettings { Name = "Default" });

    private static JsonObject Params(string name, JsonObject arguments)
        => new()
        {
            ["name"] = name,
            ["arguments"] = arguments,
        };

    private static JsonObject Meta()
        => new() { ["api_version"] = AgenticCommerceConstants.Versions.Acp20260417 };

    private static JsonObject Arguments(string id = null, JsonObject payload = null)
    {
        var arguments = new JsonObject { ["meta"] = Meta() };

        if (id is not null)
        {
            arguments["id"] = id;
        }

        if (payload is not null)
        {
            arguments["payload"] = payload;
        }

        return arguments;
    }
}
