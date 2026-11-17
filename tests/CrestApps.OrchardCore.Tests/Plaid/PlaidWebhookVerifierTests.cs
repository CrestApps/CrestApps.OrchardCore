using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Plaid.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Plaid;

public sealed class PlaidWebhookVerifierTests : IDisposable
{
    private static readonly DateTime _now = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly PlaidTestContext _context = new();
    private readonly PlaidWebhookVerifier _verifier;

    public PlaidWebhookVerifierTests()
    {
        var parameters = _key.ExportParameters(includePrivateParameters: false);

        _context.Handler.Answer("webhook_verification_key/get", new JsonObject
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

        _verifier = new PlaidWebhookVerifier(_context.Client, new MemoryCache(new MemoryCacheOptions()), clock.Object, NullLogger<PlaidWebhookVerifier>.Instance);
    }

    [Fact]
    public async Task AWebhookSignedByPlaidOverItsExactBody_IsAccepted_AndTheKeyIsReadOnce()
    {
        // Arrange
        var body = Body();

        // Act
        var first = await _verifier.VerifyAsync(FixedPlaidCredentials.Sandbox, Sign(body), body, TestContext.Current.CancellationToken);
        var second = await _verifier.VerifyAsync(FixedPlaidCredentials.Sandbox, Sign(body), body, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first);
        Assert.True(second);
        var call = Assert.Single(_context.Handler.CallsTo("webhook_verification_key/get"));
        Assert.Equal("key-1", call.Body["key_id"]!.GetValue<string>());
    }

    [Fact]
    public async Task AChangedBody_IsRefused()
    {
        // Arrange
        var body = Body();
        var signature = Sign(body);
        var changed = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(body).Replace("item-1", "item-2", StringComparison.Ordinal));

        // Act
        var accepted = await _verifier.VerifyAsync(FixedPlaidCredentials.Sandbox, signature, changed, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(accepted);
    }

    [Fact]
    public async Task ASignatureOlderThanFiveMinutes_IsRefused()
    {
        // Arrange
        var body = Body();

        // Act
        var accepted = await _verifier.VerifyAsync(FixedPlaidCredentials.Sandbox, Sign(body, issuedAt: _now.AddMinutes(-6)), body, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(accepted);
    }

    [Fact]
    public async Task ASignatureByAnotherKey_IsRefused()
    {
        // Arrange
        var body = Body();
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        // Act
        var accepted = await _verifier.VerifyAsync(FixedPlaidCredentials.Sandbox, Sign(body, signer: other), body, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(accepted);
    }

    [Fact]
    public async Task AnotherAlgorithmOrNoHeader_IsRefusedWithoutAskingPlaid()
    {
        // Arrange
        var body = Body();

        // Act
        var none = await _verifier.VerifyAsync(FixedPlaidCredentials.Sandbox, string.Empty, body, TestContext.Current.CancellationToken);
        var hs256 = await _verifier.VerifyAsync(FixedPlaidCredentials.Sandbox, Sign(body, alg: "HS256"), body, TestContext.Current.CancellationToken);
        var garbage = await _verifier.VerifyAsync(FixedPlaidCredentials.Sandbox, "not.a.jwt.at.all", body, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(none);
        Assert.False(hs256);
        Assert.False(garbage);
        Assert.Empty(_context.Handler.CallsTo("webhook_verification_key/get"));
    }

    [Fact]
    public async Task AnUnknownKey_IsRefused()
    {
        // Arrange
        var context = new PlaidTestContext();
        context.Handler.Error("webhook_verification_key/get", "INVALID_INPUT", "INVALID_WEBHOOK_VERIFICATION_KEY_ID");
        var clock = new Mock<IClock>();
        clock.SetupGet(entry => entry.UtcNow).Returns(_now);
        var verifier = new PlaidWebhookVerifier(context.Client, new MemoryCache(new MemoryCacheOptions()), clock.Object, NullLogger<PlaidWebhookVerifier>.Instance);
        var body = Body();

        // Act
        var accepted = await verifier.VerifyAsync(FixedPlaidCredentials.Sandbox, Sign(body), body, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(accepted);
    }

    public void Dispose()
        => _key.Dispose();

    private static byte[] Body()
        => Encoding.UTF8.GetBytes("{\n  \"environment\": \"sandbox\",\n  \"item_id\": \"item-1\",\n  \"webhook_code\": \"USER_PERMISSION_REVOKED\",\n  \"webhook_type\": \"ITEM\"\n}");

    private string Sign(byte[] body, DateTime? issuedAt = null, ECDsa signer = null, string alg = "ES256")
    {
        var header = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(new JsonObject { ["alg"] = alg, ["kid"] = "key-1", ["typ"] = "JWT" }.ToJsonString()));
        var claims = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(new JsonObject
        {
            ["iat"] = new DateTimeOffset(issuedAt ?? _now.AddSeconds(-30)).ToUnixTimeSeconds(),
            ["request_body_sha256"] = Convert.ToHexStringLower(SHA256.HashData(body)),
        }.ToJsonString()));
        var signature = (signer ?? _key).SignData(Encoding.ASCII.GetBytes(header + "." + claims), HashAlgorithmName.SHA256);

        return header + "." + claims + "." + Base64Url.EncodeToString(signature);
    }
}
