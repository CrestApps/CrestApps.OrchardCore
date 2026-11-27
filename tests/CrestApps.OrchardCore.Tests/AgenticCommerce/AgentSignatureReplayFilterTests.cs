using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce;
using CrestApps.OrchardCore.AgenticCommerce.Acp;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Services;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using CrestApps.OrchardCore.Api;
using CrestApps.OrchardCore.Commerce.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// SEC-C9, through the request filters and the MCP binding that run the replay guard: a signature is remembered by what
/// it signed, so another spelling of a used signature (padding, a renamed label) is still a replay; only a signature
/// that was verified and is bound to a time is remembered; a UCP signature needs a <c>created</c> time unless an
/// idempotent endpoint answers its signed key; and an ACP MCP signature covers the checkout it acts on.
/// </summary>
public sealed class AgentSignatureReplayFilterTests : IDisposable
{
    private const string ApiKey = "acp_test_key";
    private const string Timestamp = "2026-11-10T12:00:00Z";
    private const string Body = "{\"payment_data\":{\"token\":\"tok_1\"}}";
    private const string ProfileUrl = "https://agent.example/.well-known/ucp";
    private const string CompletePath = "/acp/checkout_sessions/cs_1/complete";

    private static readonly DateTime _now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly object _passed = new();

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private IDistributedCache _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    private DateTime _clockNow = _now;

    private IDistributedLock _distributedLock = new LocalLock(NullLogger<LocalLock>.Instance);

    public void Dispose()
        => _key.Dispose();

    [Fact]
    public async Task AcpFilter_APaddedCopyOfAUsedSignature_WithAnotherIdempotencyKey_IsRefused()
    {
        // Arrange: base64url with or without padding decodes to the same signature, and Idempotency-Key isn't signed.
        var platform = AcpPlatform(AcpRequestSignature.BoundVersion);
        var signature = SignAcp("POST", CompletePath, Body);

        // Act
        var first = await InvokeAcpAsync(platform, "POST", CompletePath, Body, signature, "key-1");
        var padded = await InvokeAcpAsync(platform, "POST", CompletePath, Body, signature + "==", "key-2");

        // Assert
        Assert.Same(_passed, first);
        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCode(padded));
    }

    [Fact]
    public async Task AcpFilter_TheSameSignatureRetriedWithTheSameIdempotencyKey_IsLetThroughToTheIdempotencyLayer()
    {
        // Arrange
        var platform = AcpPlatform(AcpRequestSignature.BoundVersion);
        var signature = SignAcp("POST", CompletePath, Body);

        // Act
        var first = await InvokeAcpAsync(platform, "POST", CompletePath, Body, signature, "key-1");
        var retried = await InvokeAcpAsync(platform, "POST", CompletePath, Body, signature, "key-1");
        var otherKey = await InvokeAcpAsync(platform, "POST", CompletePath, Body, signature, "key-2");

        // Assert
        Assert.Same(_passed, first);
        Assert.Same(_passed, retried);
        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCode(otherKey));
    }

    [Fact]
    public async Task AcpFilter_TheSameSignatureRetriedLongAfter_IsRefused_EvenWithinAWideClockSkew()
    {
        // Round 2 (Mild): ACP let the same idempotent request through again for twice the clock skew (up to two hours),
        // and the idempotency layer keeps no failed answer, so a copy sent much later ran the request again.
        _cache = new ClockCache(() => _clockNow);
        var platform = AcpPlatform(AcpRequestSignature.BoundVersion);
        platform.AcpSignatureClockSkewSeconds = 3600;
        var signature = SignAcp("POST", CompletePath, Body);

        // Act
        var first = await InvokeAcpAsync(platform, "POST", CompletePath, Body, signature, "key-1");
        _clockNow = _now.AddMinutes(5);
        var soonAfter = await InvokeAcpAsync(platform, "POST", CompletePath, Body, signature, "key-1");
        _clockNow = _now.AddMinutes(30);
        var muchLater = await InvokeAcpAsync(platform, "POST", CompletePath, Body, signature, "key-1");

        // Assert
        Assert.Same(_passed, first);
        Assert.Same(_passed, soonAfter);
        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCode(muchLater));
    }

    [Fact]
    public async Task AcpFilter_AGetWithAnIdempotencyKey_IsNotLetThroughTwice()
    {
        // Arrange: a GET has no idempotency layer to answer a repeat with its first result, so the key exempts nothing.
        var platform = AcpPlatform(AcpRequestSignature.BoundVersion);
        var signature = AcpRequestSignature.Sign(AcpRequestSignature.GetBoundSigningInput(Timestamp, "GET", "/acp/checkout_sessions/cs_1", []), _key);

        // Act
        var first = await InvokeAcpAsync(platform, "GET", "/acp/checkout_sessions/cs_1", body: null, signature, "key-1");
        var replayed = await InvokeAcpAsync(platform, "GET", "/acp/checkout_sessions/cs_1", body: null, signature, "key-1");

        // Assert
        Assert.Same(_passed, first);
        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCode(replayed));
    }

    [Fact]
    public async Task AcpMcp_AReadWithAnIdempotencyKey_IsNotLetThroughTwice()
    {
        // Arrange: get_checkout_session doesn't run through the MCP idempotency layer.
        var platform = AcpPlatform(AcpRequestSignature.BoundVersion);
        var signature = AcpRequestSignature.Sign(
            AcpRequestSignature.GetBoundSigningInput(Timestamp, AcpSignatureCheck.McpMethod, $"{AcpMcpEndpoints.Tools.Get}/cs_1", []),
            _key);

        // Act
        var first = await InvokeAcpMcpAsync(platform, AcpMcpEndpoints.Tools.Get, "cs_1", signature, "key-1");
        var replayed = await InvokeAcpMcpAsync(platform, AcpMcpEndpoints.Tools.Get, "cs_1", signature, "key-1");

        // Assert
        Assert.NotEqual(AcpSignatureCheck.ErrorCode, ErrorCode(first));
        Assert.Equal(AcpSignatureCheck.ErrorCode, ErrorCode(replayed));
    }

    [Fact]
    public async Task AcpFilter_ALockThatCantBeTaken_AnswersARetryableError_NotAReplay()
    {
        // Arrange
        var locks = new Mock<IDistributedLock>();

        locks.Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, false));
        _distributedLock = locks.Object;

        var platform = AcpPlatform(AcpRequestSignature.BoundVersion);

        // Act
        var result = await InvokeAcpAsync(platform, "POST", CompletePath, Body, SignAcp("POST", CompletePath, Body), "key-1");

        // Assert
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, StatusCode(result));
    }

    [Fact]
    public async Task AcpFilter_ASignatureThePlatformDoesntVerify_IsNotRemembered()
    {
        // Arrange: no key and signing not required, so the headers are ignored and nothing was verified.
        var platform = new AgentPlatform { ItemId = "platform-1", Enabled = true, Protocols = [AgenticCommerceConstants.Protocols.Acp] };

        // Act
        var first = await InvokeAcpAsync(platform, "POST", CompletePath, Body, "c2ln", "key-1");
        var second = await InvokeAcpAsync(platform, "POST", CompletePath, Body, "c2ln", "key-2");

        // Assert
        Assert.Same(_passed, first);
        Assert.Same(_passed, second);
    }

    [Fact]
    public async Task AcpFilter_AVersion1Poll_SignedTheSameEachTime_IsNotRefused()
    {
        // Arrange: a version 1 signature covers the body only (nothing for a GET), so a deterministic signer polling a
        // checkout sends the same signature each time; it carries no time to make it single-use.
        var platform = AcpPlatform(AcpRequestSignature.BodyOnlyVersion);
        var signature = AcpRequestSignature.Sign([], _key);

        // Act
        var first = await InvokeAcpAsync(platform, "GET", "/acp/checkout_sessions/cs_1", body: null, signature, idempotencyKey: null);
        var second = await InvokeAcpAsync(platform, "GET", "/acp/checkout_sessions/cs_1", body: null, signature, idempotencyKey: null);

        // Assert
        Assert.Same(_passed, first);
        Assert.Same(_passed, second);
    }

    [Fact]
    public async Task AcpMcp_AVersion2SignatureForOneCheckout_IsRefusedForAnother()
    {
        // Arrange: the checkout id is a sibling of the payload, so it is signed through the path: tool/id.
        var platform = AcpPlatform(AcpRequestSignature.BoundVersion);
        var signature = AcpRequestSignature.Sign(
            AcpRequestSignature.GetBoundSigningInput(Timestamp, AcpSignatureCheck.McpMethod, $"{AcpMcpEndpoints.Tools.Cancel}/cs_1", []),
            _key);

        // Act
        var swapped = await InvokeAcpMcpAsync(platform, AcpMcpEndpoints.Tools.Cancel, "cs_2", signature, "key-1");
        var same = await InvokeAcpMcpAsync(platform, AcpMcpEndpoints.Tools.Cancel, "cs_1", signature, "key-2");

        // Assert
        Assert.Equal(AcpSignatureCheck.ErrorCode, ErrorCode(swapped));
        Assert.NotEqual(AcpSignatureCheck.ErrorCode, ErrorCode(same));
    }

    [Fact]
    public async Task UcpFilter_AUsedSignatureUnderAnotherLabel_IsRefused()
    {
        // Arrange: the label isn't part of the signature base, so sig2 verifies exactly as sig1 did.
        var (input, signature) = SignUcp(HttpMethods.Get, "/ucp/checkout-sessions/cs_1", body: null, idempotencyKey: null, created: true);

        // Act
        var first = await InvokeUcpAsync(HttpMethods.Get, "/ucp/checkout-sessions/cs_1", body: null, idempotencyKey: null, input, signature, idempotentEndpoint: false);
        var relabeled = await InvokeUcpAsync(HttpMethods.Get, "/ucp/checkout-sessions/cs_1", body: null, idempotencyKey: null, input.Replace("sig1=", "sig2=", StringComparison.Ordinal), signature.Replace("sig1=", "sig2=", StringComparison.Ordinal), idempotentEndpoint: false);

        // Assert: the first got past the signature (and stopped at the profile's unsupported version).
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, StatusCode(first));
        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCode(relabeled));
    }

    [Fact]
    public async Task UcpFilter_ASignatureWithNeitherCreatedNorAnIdempotencyKey_IsRefused()
    {
        // Arrange: nothing in it ages, so it would be valid for ever.
        var (input, signature) = SignUcp(HttpMethods.Get, "/ucp/orders/o_1", body: null, idempotencyKey: null, created: false);

        // Act
        var result = await InvokeUcpAsync(HttpMethods.Get, "/ucp/orders/o_1", body: null, idempotencyKey: null, input, signature, idempotentEndpoint: false);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCode(result));
    }

    [Fact]
    public async Task UcpFilter_ASignedIdempotencyKeyWithoutCreated_IsAcceptedOnlyWhereTheKeyIsHonored()
    {
        // Arrange: a REST POST de-duplicates by its key, so a replay gets the first answer; the MCP binding doesn't.
        var (input, signature) = SignUcp(HttpMethods.Post, "/ucp/checkout-sessions", Body, "key-1", created: false, expiresIn: TimeSpan.FromMinutes(5));

        // Act
        var idempotent = await InvokeUcpAsync(HttpMethods.Post, "/ucp/checkout-sessions", Body, "key-1", input, signature, idempotentEndpoint: true);
        var (mcpInput, mcpSignature) = SignUcp(HttpMethods.Post, "/ucp/mcp", Body, "key-1", created: false, expiresIn: TimeSpan.FromMinutes(5));
        var mcp = await InvokeUcpAsync(HttpMethods.Post, "/ucp/mcp", Body, "key-1", mcpInput, mcpSignature, idempotentEndpoint: false);

        // Assert
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, StatusCode(idempotent));
        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCode(mcp));
    }

    /// <summary>
    /// Review 10 (B4): a signed REST write without a created time was accepted on its idempotency key alone, with no
    /// expiry or a distant one. The idempotency layer forgets the key's answer after 48 hours, so the same captured request
    /// sent then created a second checkout session. The second send runs with the idempotency record and the replay guard's
    /// memory expired.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(365 * 24)]
    public async Task UcpFilter_ASignedWriteThatOutlivesItsIdempotencyRecord_IsRefused_WhenSentAgain(int? expiresInHours)
    {
        // Arrange
        var (input, signature) = SignUcp(HttpMethods.Post, "/ucp/checkout-sessions", Body, "key-1", created: false, expiresIn: expiresInHours is { } hours ? TimeSpan.FromHours(hours) : null);

        await InvokeUcpAsync(HttpMethods.Post, "/ucp/checkout-sessions", Body, "key-1", input, signature, idempotentEndpoint: true);

        _clockNow = _now.Add(UcpEndpoints.Idempotency.Lifetime).AddHours(1);
        _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

        // Act
        var replayed = await InvokeUcpAsync(HttpMethods.Post, "/ucp/checkout-sessions", Body, "key-1", input, signature, idempotentEndpoint: true);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCode(replayed));
    }

    /// <summary>
    /// A write signed with a short expiry and no created time is still accepted, and is refused once it has expired, well
    /// before its idempotency record is forgotten.
    /// </summary>
    [Fact]
    public async Task UcpFilter_ASignedWriteWithAShortExpiry_IsAccepted_AndRefusedAfterItExpires()
    {
        // Arrange
        var (input, signature) = SignUcp(HttpMethods.Post, "/ucp/checkout-sessions", Body, "key-1", created: false, expiresIn: TimeSpan.FromHours(1));

        // Act
        var first = await InvokeUcpAsync(HttpMethods.Post, "/ucp/checkout-sessions", Body, "key-1", input, signature, idempotentEndpoint: true);

        _clockNow = _now.AddHours(2);
        _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

        var later = await InvokeUcpAsync(HttpMethods.Post, "/ucp/checkout-sessions", Body, "key-1", input, signature, idempotentEndpoint: true);

        // Assert
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, StatusCode(first));
        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCode(later));
    }

    /// <summary>
    /// Review 11 (C7): the guard remembered a signature for ten minutes, though one without created can be valid for 24
    /// hours, and let the same idempotent request through again whenever it came. The idempotency layer keeps only a
    /// final answer, so a captured write whose first attempt was answered with an error (here the 422 a request gets past
    /// its signature to) ran again when it was sent hours later. The cache expires entries by the test clock.
    /// </summary>
    [Theory]
    [InlineData(3)]
    [InlineData(20)]
    public async Task UcpFilter_ASignedWriteWhoseFirstAttemptFailed_IsRefused_WhenSentAgainHoursLater(int hoursLater)
    {
        // Arrange
        _cache = new ClockCache(() => _clockNow);

        var (input, signature) = SignUcp(HttpMethods.Post, "/ucp/checkout-sessions", Body, "key-1", created: false, expiresIn: TimeSpan.FromHours(23));

        var first = await InvokeUcpAsync(HttpMethods.Post, "/ucp/checkout-sessions", Body, "key-1", input, signature, idempotentEndpoint: true);

        _clockNow = _now.AddMinutes(2);

        var retried = await InvokeUcpAsync(HttpMethods.Post, "/ucp/checkout-sessions", Body, "key-1", input, signature, idempotentEndpoint: true);

        _clockNow = _now.AddHours(hoursLater);

        // Act
        var replayed = await InvokeUcpAsync(HttpMethods.Post, "/ucp/checkout-sessions", Body, "key-1", input, signature, idempotentEndpoint: true);

        // Assert: a retry right after the failure still gets past the signature; the copy sent hours later doesn't.
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, StatusCode(first));
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, StatusCode(retried));
        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCode(replayed));
    }

    [Fact]
    public async Task UcpFilter_AnIdempotencyKeyOnAnEndpointWithoutTheIdempotencyLayer_DoesNotExemptAReplay()
    {
        // Arrange: a signed GET that carries a key; nothing answers a repeat of it with the first result.
        var (input, signature) = SignUcp(HttpMethods.Get, "/ucp/checkout-sessions/cs_1", body: null, "key-1", created: true);

        // Act
        var first = await InvokeUcpAsync(HttpMethods.Get, "/ucp/checkout-sessions/cs_1", body: null, "key-1", input, signature, idempotentEndpoint: false);
        var replayed = await InvokeUcpAsync(HttpMethods.Get, "/ucp/checkout-sessions/cs_1", body: null, "key-1", input, signature, idempotentEndpoint: false);

        // Assert
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, StatusCode(first));
        Assert.Equal(StatusCodes.Status401Unauthorized, StatusCode(replayed));
    }

    [Fact]
    public async Task UcpFilter_TwoRequestsInTheSameSecond_WithTheirOwnNonce_AreBothAccepted()
    {
        // Arrange: created has one-second resolution, so a client that polls twice in a second makes its input unique.
        var (firstInput, firstSignature) = SignUcp(HttpMethods.Get, "/ucp/checkout-sessions/cs_1", body: null, idempotencyKey: null, created: true, nonce: "n1");
        var (secondInput, secondSignature) = SignUcp(HttpMethods.Get, "/ucp/checkout-sessions/cs_1", body: null, idempotencyKey: null, created: true, nonce: "n2");

        // Act
        var first = await InvokeUcpAsync(HttpMethods.Get, "/ucp/checkout-sessions/cs_1", body: null, idempotencyKey: null, firstInput, firstSignature, idempotentEndpoint: false);
        var second = await InvokeUcpAsync(HttpMethods.Get, "/ucp/checkout-sessions/cs_1", body: null, idempotencyKey: null, secondInput, secondSignature, idempotentEndpoint: false);

        // Assert
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, StatusCode(first));
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, StatusCode(second));
    }

    [Fact]
    public async Task UcpRestEndpoints_AChangeWithoutAnIdempotencyKey_IsRefused()
    {
        // Arrange: UCP's REST binding requires Idempotency-Key on POST and PUT, the key a signature without created
        // relies on against replays.
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        };
        var ran = false;

        httpContext.Request.Method = HttpMethods.Post;
        httpContext.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(UcpEndpoints.Idempotency), "UcpCreateCheckout"));

        // Act
        var result = await new IdempotencyEndpointFilter().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ =>
        {
            ran = true;

            return ValueTask.FromResult<object>(null);
        });

        // Assert
        Assert.False(ran);
        Assert.Equal(StatusCodes.Status400BadRequest, StatusCode(result));
    }

    private AgentPlatform AcpPlatform(int version)
        => new()
        {
            ItemId = "platform-1",
            Enabled = true,
            Protocols = [AgenticCommerceConstants.Protocols.Acp],
            AcpRequireSignedRequests = true,
            AcpSignatureAlgorithm = AcpRequestSignature.ES256,
            AcpSignaturePublicKey = EcJsonWebKey.FromECDsa(_key, "agent-1", includePrivateKey: false).ToJson().ToJsonString(),
            AcpSignatureVersion = version,
        };

    private string SignAcp(string method, string path, string body)
        => AcpRequestSignature.Sign(
            AcpRequestSignature.GetBoundSigningInput(Timestamp, method, path, AcpRequestSignature.GetSigningInput(JsonNode.Parse(body))),
            _key);

    private async Task<object> InvokeAcpAsync(AgentPlatform platform, string method, string path, string body, string signature, string idempotencyKey)
    {
        var httpContext = CreateContext(CreateServices(platform), method, path, body);

        httpContext.Request.Headers["X-API-Key"] = ApiKey;
        httpContext.Request.Headers[AcpConstants.VersionHeader] = AgenticCommerceConstants.Versions.Acp20260417;
        httpContext.Request.Headers[AcpRequestSignature.SignatureHeader] = signature;
        httpContext.Request.Headers[AcpRequestSignature.TimestampHeader] = Timestamp;

        if (idempotencyKey is not null)
        {
            httpContext.Request.Headers["Idempotency-Key"] = idempotencyKey;
        }

        // Every ACP POST runs through the idempotency layer; a GET doesn't.
        if (HttpMethods.IsPost(method))
        {
            httpContext.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(AcpEndpoints.Idempotency), "AcpCompleteCheckout"));
        }

        return await new AcpRequestFilter().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ => ValueTask.FromResult(_passed));
    }

    private async Task<JsonObject> InvokeAcpMcpAsync(AgentPlatform platform, string tool, string checkoutId, string signature, string idempotencyKey)
    {
        var message = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "tools/call",
            ["params"] = new JsonObject
            {
                ["name"] = tool,
                ["arguments"] = new JsonObject
                {
                    ["meta"] = new JsonObject
                    {
                        ["api_version"] = AgenticCommerceConstants.Versions.Acp20260417,
                        ["idempotency_key"] = idempotencyKey,
                        ["signature"] = signature,
                        ["timestamp"] = Timestamp,
                    },
                    ["id"] = checkoutId,
                },
            },
        };

        var services = CreateServices(platform);
        var httpContext = CreateContext(services, HttpMethods.Post, "/" + AcpMcpEndpoints.Route, message.ToJsonString());
        var checkouts = new Mock<IAgentCheckoutService>();

        checkouts.Setup(service => service.GetAsync(It.IsAny<AgentCheckoutContext>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentCheckoutResult.Fail(AgentCheckoutFailure.NotFound, "There is no such checkout."));
        checkouts.Setup(service => service.CancelAsync(It.IsAny<AgentCheckoutContext>(), It.IsAny<string>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentCheckoutResult.Fail(AgentCheckoutFailure.NotFound, "There is no such checkout."));

        httpContext.Request.Headers["X-API-Key"] = ApiKey;

        var result = await AcpMcpEndpoints.HandleAsync(
            httpContext,
            new AcpCheckoutOperations(checkouts.Object, new AcpCheckoutMapper(checkouts.Object)),
            new AcpMcpIdempotency(_cache, _distributedLock, new ShellSettings { Name = "Default" }));

        return Assert.IsType<JsonHttpResult<JsonObject>>(result).Value;
    }

    private (string Input, string Signature) SignUcp(string method, string path, string body, string idempotencyKey, bool created, string nonce = null, TimeSpan? expiresIn = null)
    {
        var httpContext = CreateContext(new ServiceCollection().BuildServiceProvider(), method, path, body);

        AddUcpHeaders(httpContext.Request, body, idempotencyKey);

        var signed = HttpMessageSignatures.Sign(
            UcpRequestFilter.ToSignatureMessage(httpContext.Request),
            HttpMessageSignatures.DefaultRequestComponents(body is not null, hasQuery: false, hasUcpAgent: true, idempotencyKey is not null),
            "k1",
            _key,
            created: created ? new DateTimeOffset(_now).ToUnixTimeSeconds() : null,
            nonce: nonce,
            expires: expiresIn is { } lifetime ? new DateTimeOffset(_now).Add(lifetime).ToUnixTimeSeconds() : null);

        return (signed.SignatureInput, signed.Signature);
    }

    private async Task<object> InvokeUcpAsync(string method, string path, string body, string idempotencyKey, string signatureInput, string signature, bool idempotentEndpoint)
    {
        var platform = new AgentPlatform
        {
            ItemId = "platform-1",
            Enabled = true,
            Protocols = [AgenticCommerceConstants.Protocols.Ucp],
            ProfileUrl = ProfileUrl,
        };
        var httpContext = CreateContext(CreateServices(platform), method, path, body);

        AddUcpHeaders(httpContext.Request, body, idempotencyKey);
        httpContext.Request.Headers["X-API-Key"] = ApiKey;
        httpContext.Request.Headers["Signature-Input"] = signatureInput;
        httpContext.Request.Headers["Signature"] = signature;

        if (idempotentEndpoint)
        {
            httpContext.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(UcpEndpoints.Idempotency), "UcpCreateCheckout"));
        }

        return await new UcpRequestFilter().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ => ValueTask.FromResult(_passed));
    }

    private static void AddUcpHeaders(HttpRequest request, string body, string idempotencyKey)
    {
        request.Headers[UcpConstants.AgentHeader] = $"profile=\"{ProfileUrl}\"";

        if (idempotencyKey is not null)
        {
            request.Headers["Idempotency-Key"] = idempotencyKey;
        }

        if (body is not null)
        {
            request.ContentType = "application/json";
            request.Headers["Content-Digest"] = ContentDigest.ComputeSha256(Encoding.UTF8.GetBytes(body));
        }
    }

    private ServiceProvider CreateServices(AgentPlatform platform)
    {
        var platforms = new Mock<IAgentPlatformStore>();
        var factory = new Mock<IHttpClientFactory>();
        var site = new SiteSettings();
        var siteService = new Mock<ISiteService>();
        var profile = new JsonObject
        {
            // A version the store doesn't serve: a request that gets past its signature stops at 422.
            ["ucp"] = new JsonObject { ["version"] = "1999-01-01" },
            ["keys"] = new JsonArray(EcJsonWebKey.FromECDsa(_key, "k1", includePrivateKey: false).ToJson()),
        };

        platforms.Setup(store => store.FindByApiKeyHashAsync(AgentPlatformApiKeys.Hash(ApiKey), It.IsAny<CancellationToken>())).ReturnsAsync(platform);
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(new ProfileHandler(profile)));
        site.Put(new AgenticCommerceSettings());
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site);

        return new ServiceCollection()
            .AddLogging()
            .AddSingleton(_cache)
            .AddSingleton(_distributedLock)
            .AddSingleton<AgentSignatureReplayGuard>()
            .AddSingleton(platforms.Object)
            .AddSingleton(factory.Object)
            .AddSingleton(siteService.Object)
            .AddSingleton(Mock.Of<global::OrchardCore.Modules.IClock>(clock => clock.UtcNow == _clockNow))
            .AddSingleton<UcpProfileCache>()
            .AddScoped<UcpProfileClient>()
            .AddSingleton(new UcpNegotiator(null))
            .AddSingleton(new AgentPlatformAuthenticator(platforms.Object, NullLogger<AgentPlatformAuthenticator>.Instance))
            .BuildServiceProvider();
    }

    private static DefaultHttpContext CreateContext(IServiceProvider services, string method, string path, string body)
    {
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var bytes = body is null ? [] : Encoding.UTF8.GetBytes(body);

        httpContext.Request.Method = method;
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("shop.example");
        httpContext.Request.Path = path;
        httpContext.Request.Body = new MemoryStream(bytes);
        httpContext.Request.ContentLength = bytes.Length;

        return httpContext;
    }

    private static int? StatusCode(object result)
        => Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode;

    // A distributed cache whose entries expire by the test's clock rather than the machine's.
    private sealed class ClockCache : IDistributedCache
    {
        private readonly Dictionary<string, (byte[] Value, DateTime? Expires)> _entries = new(StringComparer.Ordinal);
        private readonly Func<DateTime> _now;

        public ClockCache(Func<DateTime> now)
        {
            _now = now;
        }

        public byte[] Get(string key)
            => _entries.TryGetValue(key, out var entry) && (entry.Expires is null || entry.Expires > _now()) ? entry.Value : null;

        public Task<byte[]> GetAsync(string key, CancellationToken token = default)
            => Task.FromResult(Get(key));

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
            => _entries[key] = (value, options.AbsoluteExpirationRelativeToNow is { } relative ? _now() + relative : options.AbsoluteExpiration?.UtcDateTime);

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);

            return Task.CompletedTask;
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default)
            => Task.CompletedTask;

        public void Remove(string key)
            => _entries.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);

            return Task.CompletedTask;
        }
    }

    private static string ErrorCode(JsonObject response)
        => response["error"]?["data"]?["code"]?.GetValue<string>();

    private sealed class ProfileHandler : HttpMessageHandler
    {
        private readonly JsonObject _profile;

        public ProfileHandler(JsonObject profile)
        {
            _profile = profile;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_profile.ToJsonString(), Encoding.UTF8, "application/json"),
            });
    }
}
