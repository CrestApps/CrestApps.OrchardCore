using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Plaid;
using CrestApps.OrchardCore.Plaid.Endpoints;
using CrestApps.OrchardCore.Plaid.Services;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Plaid;

public sealed class PlaidWebhookEndpointTests : IDisposable
{
    private static readonly DateTime _now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly PlaidTestContext _plaid = new();
    private readonly List<ProviderWebhookInboxDelivery> _stored = [];
    private readonly PlaidWebhookVerifier _verifier;
    private readonly Mock<IProviderWebhookInbox> _inbox = new();

    public PlaidWebhookEndpointTests()
    {
        var parameters = _key.ExportParameters(includePrivateParameters: false);

        _plaid.Handler.Answer("webhook_verification_key/get", new JsonObject
        {
            ["key"] = new JsonObject
            {
                ["alg"] = "ES256",
                ["crv"] = "P-256",
                ["kid"] = "key-1",
                ["kty"] = "EC",
                ["use"] = "sig",
                ["x"] = Base64Url.EncodeToString(parameters.Q.X),
                ["y"] = Base64Url.EncodeToString(parameters.Q.Y),
                ["created_at"] = 1700000000,
                ["expired_at"] = null,
            },
            ["request_id"] = "r",
        }.ToJsonString());

        var clock = new Mock<IClock>();
        clock.SetupGet(entry => entry.UtcNow).Returns(_now);

        _verifier = new PlaidWebhookVerifier(_plaid.Client, new MemoryCache(new MemoryCacheOptions()), clock.Object, NullLogger<PlaidWebhookVerifier>.Instance);

        _inbox
            .Setup(inbox => inbox.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderWebhookInboxDelivery, CancellationToken>((delivery, _) => _stored.Add(delivery))
            .ReturnsAsync(new ProviderWebhookInboxAcceptanceResult
            {
                Status = ProviderWebhookInboxAcceptanceStatus.Accepted,
                MessageId = "message-1",
            });
    }

    [Fact]
    public async Task ASignedWebhook_IsVerifiedOnce_StoredOnce_AndHandedToEveryHandlerFromTheInbox()
    {
        // Arrange
        var body = Encoding.UTF8.GetBytes("""{"webhook_type":"TRANSACTIONS","webhook_code":"SYNC_UPDATES_AVAILABLE","item_id":"item-7","environment":"sandbox"}""");
        var feed = new RecordingPlaidWebhookHandler();
        var other = new RecordingPlaidWebhookHandler();

        // Act
        var result = await PlaidWebhookEndpoint.HandleAsync(Request(body, Sign(body)), _plaid.Credentials, _verifier, Mock.Of<IShellHost>(), new ShellSettings(), NullLogger<PlaidWebhookVerifier>.Instance);
        var delivery = Assert.Single(_stored);
        await new PlaidWebhookInboxHandler([feed, other], NullLogger<PlaidWebhookInboxHandler>.Instance)
            .HandleAsync(delivery.Payload, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<Ok>(result);
        Assert.Equal(PlaidConstants.WebhookHandlerName, delivery.HandlerName);
        Assert.Equal(PlaidConstants.ProviderName, delivery.ProviderName);
        Assert.Single(_plaid.Handler.CallsTo("webhook_verification_key/get"));
        Assert.Single(_plaid.Handler.Calls);
        Assert.True(Assert.Single(feed.Received).Is("TRANSACTIONS", "SYNC_UPDATES_AVAILABLE"));
        Assert.Equal("item-7", Assert.Single(other.Received).Webhook.ItemId);
    }

    [Fact]
    public async Task AWebhookWithABadSignature_IsRefused_AndNeverStored()
    {
        // Arrange
        var body = Encoding.UTF8.GetBytes("""{"webhook_type":"ITEM","webhook_code":"ERROR","item_id":"item-7"}""");
        var signedOther = Sign(Encoding.UTF8.GetBytes("""{"webhook_type":"ITEM","webhook_code":"LOGIN_REPAIRED","item_id":"item-7"}"""));

        // Act
        var result = await PlaidWebhookEndpoint.HandleAsync(Request(body, signedOther), _plaid.Credentials, _verifier, Mock.Of<IShellHost>(), new ShellSettings(), NullLogger<PlaidWebhookVerifier>.Instance);

        // Assert
        Assert.IsType<BadRequest>(result);
        Assert.Empty(_stored);
    }

    public void Dispose()
        => _key.Dispose();

    private DefaultHttpContext Request(byte[] body, string signature)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton(_inbox.Object)
                .BuildServiceProvider(),
        };

        context.Request.Method = HttpMethods.Post;
        context.Request.Body = new MemoryStream(body);
        context.Request.ContentLength = body.Length;
        context.Request.Headers["Plaid-Verification"] = signature;

        return context;
    }

    private string Sign(byte[] body)
    {
        var header = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(new JsonObject { ["alg"] = "ES256", ["kid"] = "key-1", ["typ"] = "JWT" }.ToJsonString()));
        var claims = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(new JsonObject
        {
            ["iat"] = new DateTimeOffset(_now.AddSeconds(-30)).ToUnixTimeSeconds(),
            ["request_body_sha256"] = Convert.ToHexStringLower(SHA256.HashData(body)),
        }.ToJsonString()));
        var signature = _key.SignData(Encoding.ASCII.GetBytes(header + "." + claims), HashAlgorithmName.SHA256);

        return header + "." + claims + "." + Base64Url.EncodeToString(signature);
    }
}
