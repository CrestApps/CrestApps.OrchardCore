using System.Security.Cryptography;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Acp;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrchardCore.Locking;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// SEC-C9: an agent's signed request can't be replayed or pointed at another target. ACP signature version 2 signs the
/// timestamp, method and path with the body; a signature is accepted once in its window, again only for the same
/// idempotent request; and a UCP signature must cover the method, authority, path and body digest, with a fresh
/// <c>created</c> when it has one.
/// </summary>
public sealed class AgentSignatureReplayTests : IDisposable
{
    private static readonly DateTimeOffset _now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private const string Timestamp = "2026-11-10T12:00:00Z";

    private const string Body = "{\"line_items\":[{\"id\":\"sku-1\",\"quantity\":2}]}";

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public void Dispose()
        => _key.Dispose();

    [Fact]
    public void AcpVersion2_SignatureForAnotherPath_IsRefused()
    {
        // Arrange
        var platform = Platform(version: AcpRequestSignature.BoundVersion);
        var signature = SignBound("POST", "/acp/checkout_sessions/cs_1/complete");

        // Act
        var swapped = AcpSignatureCheck.Check(platform, Input(), signature, Timestamp, "POST", "/acp/checkout_sessions/cs_2/complete", _now);
        var same = AcpSignatureCheck.Check(platform, Input(), signature, Timestamp, "POST", "/acp/checkout_sessions/cs_1/complete", _now);

        // Assert
        Assert.NotNull(swapped);
        Assert.Null(same);
    }

    [Fact]
    public void AcpVersion2_BodyOnlySignature_IsRefused()
    {
        // Arrange: a platform that moved to version 2 no longer accepts the body-only signature.
        var platform = Platform(version: AcpRequestSignature.BoundVersion);
        var bodyOnly = AcpRequestSignature.Sign(Input(), _key);

        // Act
        var error = AcpSignatureCheck.Check(platform, Input(), bodyOnly, Timestamp, "POST", "/acp/checkout_sessions", _now);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void AcpUnversionedPlatform_KeepsTheBodyOnlySignature()
    {
        // Arrange: a platform saved before versions existed keeps working until it moves.
        var platform = Platform(version: null);

        // Act
        var error = AcpSignatureCheck.Check(platform, Input(), AcpRequestSignature.Sign(Input(), _key), Timestamp, "POST", "/acp/checkout_sessions", _now);

        // Assert
        Assert.Null(error);
    }

    [Fact]
    public async Task ReplayGuard_TheSameSignatureTwice_IsRefusedTheSecondTime()
    {
        // Arrange
        var guard = CreateGuard();

        // Act
        var first = await guard.TryUseAsync("platform-1", "signed-input-1"u8.ToArray(), idempotencyKey: null, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
        var replayed = await guard.TryUseAsync("platform-1", "signed-input-1"u8.ToArray(), idempotencyKey: null, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first);
        Assert.False(replayed);
    }

    [Fact]
    public async Task ReplayGuard_TheSameIdempotentRequestRetried_IsAllowed_ButNotWithAnotherKey()
    {
        // Arrange
        var guard = CreateGuard();

        await guard.TryUseAsync("platform-1", "signed-input-1"u8.ToArray(), "key-1", TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        // Act
        var retried = await guard.TryUseAsync("platform-1", "signed-input-1"u8.ToArray(), "key-1", TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
        var otherKey = await guard.TryUseAsync("platform-1", "signed-input-1"u8.ToArray(), "key-2", TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(retried);
        Assert.False(otherKey);
    }

    [Theory]
    [InlineData("@authority")]
    [InlineData("@path")]
    [InlineData("@method")]
    [InlineData("content-digest")]
    public void UcpPolicy_ASignatureMissingARequiredComponent_IsRefused(string missing)
    {
        // Arrange
        var components = new[] { "@method", "@authority", "@path", "content-digest", "content-type" }.Where(component => component != missing).ToArray();
        var verification = new HttpSignatureVerification { Succeeded = true, CoveredComponents = components };

        // Act
        var error = UcpSignaturePolicy.Check(verification, hasBody: true, idempotencyKey: null, idempotent: true, _now);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void UcpPolicy_AStaleCreatedTime_IsRefused()
    {
        // Arrange
        var verification = new HttpSignatureVerification
        {
            Succeeded = true,
            CoveredComponents = ["@method", "@authority", "@path", "content-digest"],
            Created = _now.AddMinutes(-6),
        };

        // Act
        var error = UcpSignaturePolicy.Check(verification, hasBody: true, idempotencyKey: null, idempotent: true, _now);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void UcpPolicy_ACompleteSignature_IsAccepted()
    {
        // Arrange
        var verification = new HttpSignatureVerification
        {
            Succeeded = true,
            CoveredComponents = ["@method", "@authority", "@path", "idempotency-key", "content-digest", "content-type"],
            Created = _now.AddSeconds(-30),
        };

        // Act
        var error = UcpSignaturePolicy.Check(verification, hasBody: true, idempotencyKey: "key-1", idempotent: true, _now);

        // Assert
        Assert.Null(error);
    }

    private static AgentSignatureReplayGuard CreateGuard()
        => new(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), new LocalLock(NullLogger<LocalLock>.Instance));

    private AgentPlatform Platform(int? version)
        => new()
        {
            AcpRequireSignedRequests = true,
            AcpSignatureAlgorithm = AcpRequestSignature.ES256,
            AcpSignaturePublicKey = EcJsonWebKey.FromECDsa(_key, "agent-1", includePrivateKey: false).ToJson().ToJsonString(),
            AcpSignatureVersion = version,
        };

    private string SignBound(string method, string path)
        => AcpRequestSignature.Sign(AcpRequestSignature.GetBoundSigningInput(Timestamp, method, path, Input()), _key);

    private static byte[] Input()
        => AcpRequestSignature.GetSigningInput(JsonNode.Parse(Body));
}
