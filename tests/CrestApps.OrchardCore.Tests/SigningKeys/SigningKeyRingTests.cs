using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.SigningKeys;
using CrestApps.OrchardCore.SigningKeys.Core;
using CrestApps.OrchardCore.SigningKeys.Core.Models;
using CrestApps.OrchardCore.SigningKeys.Core.Services;
using CrestApps.OrchardCore.Tests.AgenticCommerce;
using CrestApps.OrchardCore.Tests.Learning;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrchardCore.Secrets;

namespace CrestApps.OrchardCore.Tests.SigningKeys;

/// <summary>
/// The shared signing keys: a key set's key is made the first time it is needed and kept as a secret of Orchard Core
/// Secrets (an RsaKeySecret for RS256, a TextSecret holding the private JSON Web Key for ES256); only its id, public half
/// and dates are in the ring's document. It is published without its private part, rotated at the set's rotation age,
/// a retired key stays published for the set's retired lifetime, and the secrets of keys retired long ago are removed.
/// Keys a module kept before can be imported with their ids and dates.
/// </summary>
public sealed class SigningKeyRingTests
{
    private const string RsaSet = "Tests.Rsa";
    private const string EcSet = "Tests.Ec";

    private static readonly DateTime _now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryDocumentManager<SigningKeysDocument> _documents = new();
    private readonly InMemorySecretManager _secrets = new();
    private readonly TestClock _clock = new(_now);
    private readonly SigningKeyOptions _options = new();

    public SigningKeyRingTests()
    {
        var rsa = _options.GetOrAdd(RsaSet);
        rsa.Algorithm = SigningKeyAlgorithms.RS256;
        rsa.KeyIdPrefix = "platform";
        rsa.RotationAge = TimeSpan.FromDays(90);
        rsa.RetiredLifetime = TimeSpan.FromDays(7);

        var ec = _options.GetOrAdd(EcSet);
        ec.Algorithm = SigningKeyAlgorithms.ES256;
        ec.KeyIdPrefix = "merchant";
    }

    [Fact]
    public async Task GetSigningKeyAsync_TheFirstTime_MakesAnRsaKeySecretAndARecordWithoutThePrivateKey()
    {
        // Arrange
        var ring = CreateRing();

        // Act
        var key = await ring.GetSigningKeyAsync(RsaSet, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SigningKeyAlgorithms.RS256, key.Algorithm);
        Assert.StartsWith("platform-20261101-", key.Kid, StringComparison.Ordinal);
        Assert.NotNull(key.PrivateJwk["d"]);
        Assert.Null(key.PublicJwk["d"]);

        var record = Assert.Single(_documents.Stored.KeySets[RsaSet]);
        Assert.Equal(key.Kid, record.Kid);
        Assert.Null(record.RetiredUtc);
        Assert.DoesNotContain("\"d\"", record.PublicJwk, StringComparison.Ordinal);

        Assert.Equal(typeof(RsaKeySecret), _secrets.TypeOf(record.SecretName));

        var secret = await _secrets.GetSecretAsync<RsaKeySecret>(record.SecretName);
        Assert.True(secret.IncludesPrivateKey);
        Assert.Equal(2048, secret.KeySize);
    }

    [Fact]
    public async Task GetSigningKeyAsync_Es256_KeepsThePrivateJwkInATextSecret()
    {
        // Arrange
        var ring = CreateRing();

        // Act
        var key = await ring.GetSigningKeyAsync(EcSet, TestContext.Current.CancellationToken);

        // Assert
        var record = Assert.Single(_documents.Stored.KeySets[EcSet]);
        Assert.Equal(typeof(TextSecret), _secrets.TypeOf(record.SecretName));
        Assert.Equal("EC", key.PrivateJwk["kty"]?.GetValue<string>());
        Assert.Equal("P-256", key.PrivateJwk["crv"]?.GetValue<string>());
        Assert.True(Verifies(key.Algorithm, Sign(key), key.PublicJwk));
    }

    [Fact]
    public async Task GetSigningKeyAsync_Again_FromAnotherScope_ReturnsTheSameKeyFromItsSecret()
    {
        // Arrange
        var first = await CreateRing().GetSigningKeyAsync(RsaSet, TestContext.Current.CancellationToken);

        // Act
        var second = await CreateRing().GetSigningKeyAsync(RsaSet, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(first.Kid, second.Kid);
        Assert.Equal(first.PrivateJwk["d"]?.GetValue<string>(), second.PrivateJwk["d"]?.GetValue<string>());
        Assert.Equal(1, _documents.Updates);
        Assert.Equal(1, _secrets.Saves);
    }

    [Fact]
    public async Task GetPublishedKeysAsync_PublishesThePublicHalf_ThatVerifiesWhatTheKeySigns()
    {
        // Arrange
        var ring = CreateRing();

        // Act
        var published = await ring.GetPublishedKeysAsync(RsaSet, TestContext.Current.CancellationToken);
        var key = await ring.GetSigningKeyAsync(RsaSet, TestContext.Current.CancellationToken);

        // Assert
        var jwk = Assert.Single(published);
        Assert.Equal(key.Kid, jwk["kid"]?.GetValue<string>());
        Assert.Equal("sig", jwk["use"]?.GetValue<string>());
        Assert.Equal("RS256", jwk["alg"]?.GetValue<string>());
        Assert.Null(jwk["d"]);
        Assert.Null(jwk["p"]);
        Assert.True(Verifies(key.Algorithm, Sign(key), jwk));
    }

    [Fact]
    public async Task RotateAsync_KeepsTheOldKeyPublished_AndTheNewOneSigns()
    {
        // Arrange
        var old = await CreateRing().GetSigningKeyAsync(RsaSet, TestContext.Current.CancellationToken);
        var ring = CreateRing();

        // Act
        var rotated = await ring.RotateAsync(RsaSet, TestContext.Current.CancellationToken);
        var current = await ring.GetSigningKeyAsync(RsaSet, TestContext.Current.CancellationToken);
        var published = await CreateRing().GetPublishedKeysAsync(RsaSet, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(old.Kid, rotated.Kid);
        Assert.Equal(rotated.Kid, current.Kid);
        Assert.Equal([old.Kid, current.Kid], published.Select(jwk => jwk["kid"]?.GetValue<string>()));
        Assert.True(Verifies(old.Algorithm, Sign(old), published[0]));
        Assert.Equal(_now, _documents.Stored.KeySets[RsaSet][0].RetiredUtc);
    }

    [Fact]
    public async Task GetPublishedKeysAsync_AfterTheRetiredLifetime_NoLongerPublishesTheRetiredKey()
    {
        // Arrange
        var old = await CreateRing().GetSigningKeyAsync(RsaSet, TestContext.Current.CancellationToken);

        await CreateRing().RotateAsync(RsaSet, TestContext.Current.CancellationToken);

        var weekLater = await CreateRing().GetPublishedKeysAsync(RsaSet, TestContext.Current.CancellationToken);

        _clock.UtcNow = _now.AddDays(8);

        // Act
        var published = await CreateRing().GetPublishedKeysAsync(RsaSet, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, weekLater.Count);

        var jwk = Assert.Single(published);
        Assert.NotEqual(old.Kid, jwk["kid"]?.GetValue<string>());
    }

    [Fact]
    public async Task RotateAsync_RemovesTheSecretsOfKeysRetiredMoreThanFourLifetimesAgo()
    {
        // Arrange
        await CreateRing().GetSigningKeyAsync(RsaSet, TestContext.Current.CancellationToken);
        await CreateRing().RotateAsync(RsaSet, TestContext.Current.CancellationToken);

        var oldSecret = _documents.Stored.KeySets[RsaSet][0].SecretName;

        _clock.UtcNow = _now.AddDays(29);

        // Act
        await CreateRing().RotateAsync(RsaSet, TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(oldSecret, _secrets.Names);
        Assert.Equal(2, _documents.Stored.KeySets[RsaSet].Count);
        Assert.Equal(2, _secrets.Names.Count);
    }

    [Fact]
    public async Task RotateIfDueAsync_BeforeTheRotationAge_KeepsTheKey()
    {
        // Arrange
        var key = await CreateRing().GetSigningKeyAsync(RsaSet, TestContext.Current.CancellationToken);

        _clock.UtcNow = _now.AddDays(89);

        var ring = CreateRing();

        // Act
        var rotated = await ring.RotateIfDueAsync(RsaSet, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(rotated);
        Assert.Equal(key.Kid, (await ring.GetSigningKeyAsync(RsaSet, TestContext.Current.CancellationToken)).Kid);
    }

    [Fact]
    public async Task RotateIfDueAsync_AtTheRotationAge_Rotates()
    {
        // Arrange
        var key = await CreateRing().GetSigningKeyAsync(RsaSet, TestContext.Current.CancellationToken);

        _clock.UtcNow = _now.AddDays(90);

        var ring = CreateRing();

        // Act
        var rotated = await ring.RotateIfDueAsync(RsaSet, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(rotated);
        Assert.NotEqual(key.Kid, (await ring.GetSigningKeyAsync(RsaSet, TestContext.Current.CancellationToken)).Kid);
        Assert.Single(_documents.Stored.KeySets[RsaSet], record => record.RetiredUtc is null);
    }

    [Fact]
    public async Task RotateIfDueAsync_WithoutAKey_MakesOne()
    {
        // Arrange
        var ring = CreateRing();

        // Act
        var rotated = await ring.RotateIfDueAsync(EcSet, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(rotated);
        Assert.Single(_documents.Stored.KeySets[EcSet]);
    }

    [Fact]
    public async Task GetSigningKeyAsync_WhenTheSecretIsGone_MakesANewKey()
    {
        // Arrange
        var lost = await CreateRing().GetSigningKeyAsync(RsaSet, TestContext.Current.CancellationToken);

        await _secrets.RemoveSecretAsync(_documents.Stored.KeySets[RsaSet][0].SecretName);

        // Act
        var key = await CreateRing().GetSigningKeyAsync(RsaSet, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(lost.Kid, key.Kid);
        Assert.NotNull(key.PrivateJwk["d"]);
    }

    [Fact]
    public async Task HasKeysAsync_DoesNotMakeAKey()
    {
        // Arrange
        var ring = CreateRing();

        // Act
        var before = await ring.HasKeysAsync(RsaSet, TestContext.Current.CancellationToken);

        await ring.GetSigningKeyAsync(RsaSet, TestContext.Current.CancellationToken);

        var after = await CreateRing().HasKeysAsync(RsaSet, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(before);
        Assert.True(after);
        Assert.Equal(1, _documents.Updates);
    }

    [Fact]
    public async Task ImportAsync_KeepsTheIdsDatesAndPublishedHalves_AndTheCurrentKeySigns()
    {
        // Arrange
        var current = Imported("legacy-current", _now.AddDays(-10), retiredUtc: null);
        var recent = Imported("legacy-recent", _now.AddDays(-100), retiredUtc: _now.AddDays(-2));
        var ring = CreateRing();

        // Act
        var count = await ring.ImportAsync(EcSet, [recent, current], TestContext.Current.CancellationToken);
        var published = await CreateRing().GetPublishedKeysAsync(EcSet, TestContext.Current.CancellationToken);
        var key = await CreateRing().GetSigningKeyAsync(EcSet, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, count);
        Assert.Equal([recent.PublicJwk.ToJsonString(), current.PublicJwk.ToJsonString()], published.Select(jwk => jwk.ToJsonString()));
        Assert.Equal("legacy-current", key.Kid);
        Assert.Equal(current.PrivateJwk["d"]?.GetValue<string>(), key.PrivateJwk["d"]?.GetValue<string>());
        Assert.Equal(_now.AddDays(-10), _documents.Stored.KeySets[EcSet].Single(record => record.Kid == "legacy-current").CreatedUtc);
    }

    [Fact]
    public async Task ImportAsync_Twice_ChangesNothing()
    {
        // Arrange
        var current = Imported("legacy-current", _now.AddDays(-10), retiredUtc: null);

        await CreateRing().ImportAsync(EcSet, [current], TestContext.Current.CancellationToken);

        var updates = _documents.Updates;

        // Act
        var count = await CreateRing().ImportAsync(EcSet, [current], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, count);
        Assert.Equal(updates, _documents.Updates);
        Assert.Single(_documents.Stored.KeySets[EcSet]);
    }

    [Fact]
    public async Task RotateIfDueAsync_WithAnImportSource_ImportsItsKeysBeforeMakingOne()
    {
        // Arrange: the rotation task runs before any request reads the set.
        var current = Imported("legacy-current", _now.AddDays(-10), retiredUtc: null);
        var source = new TestImportSource(EcSet, current);
        var ring = CreateRing(source);

        // Act
        var rotated = await ring.RotateIfDueAsync(EcSet, TestContext.Current.CancellationToken);
        var key = await CreateRing(source).GetSigningKeyAsync(EcSet, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(rotated);
        Assert.Equal("legacy-current", key.Kid);
        Assert.Single(_documents.Stored.KeySets[EcSet]);
        Assert.Equal(1, source.Reads);
    }

    [Fact]
    public async Task GetSigningKeyAsync_ForAKeySetNobodyRegistered_Throws()
    {
        // Arrange
        var ring = CreateRing();

        // Act and assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => ring.GetSigningKeyAsync("Tests.Unknown", TestContext.Current.CancellationToken));
    }

    private SigningKeyRing CreateRing(params ISigningKeyImportSource[] importSources)
        => new(_documents, _secrets, new RecordingLock(), Options.Create(_options), importSources, _clock, NullLogger<SigningKeyRing>.Instance);

    private static SigningKeyImport Imported(string kid, DateTime createdUtc, DateTime? retiredUtc)
    {
        var privateJwk = SigningKeyJwk.Create(SigningKeyAlgorithms.ES256, kid);

        return new SigningKeyImport
        {
            Kid = kid,
            PrivateJwk = privateJwk,
            PublicJwk = SigningKeyJwk.PublicOf(privateJwk),
            CreatedUtc = createdUtc,
            RetiredUtc = retiredUtc,
        };
    }

    private static byte[] Sign(SigningKey key)
    {
        var data = Encoding.UTF8.GetBytes("signed by " + key.Kid);

        if (key.Algorithm == SigningKeyAlgorithms.RS256)
        {
            using var rsa = Rsa(key.PrivateJwk);

            return rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        using var ecdsa = Ec(key.PrivateJwk);

        return ecdsa.SignData(data, HashAlgorithmName.SHA256);
    }

    private static bool Verifies(string algorithm, byte[] signature, JsonObject publicJwk)
    {
        var data = Encoding.UTF8.GetBytes("signed by " + publicJwk["kid"]?.GetValue<string>());

        if (algorithm == SigningKeyAlgorithms.RS256)
        {
            using var rsa = Rsa(publicJwk);

            return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        using var ecdsa = Ec(publicJwk);

        return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256);
    }

    private static RSA Rsa(JsonObject jwk)
    {
        var rsa = RSA.Create();

        rsa.ImportParameters(new RSAParameters
        {
            Modulus = Bytes(jwk, "n"),
            Exponent = Bytes(jwk, "e"),
            D = Bytes(jwk, "d"),
            P = Bytes(jwk, "p"),
            Q = Bytes(jwk, "q"),
            DP = Bytes(jwk, "dp"),
            DQ = Bytes(jwk, "dq"),
            InverseQ = Bytes(jwk, "qi"),
        });

        return rsa;
    }

    private static ECDsa Ec(JsonObject jwk)
        => ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = Bytes(jwk, "x"), Y = Bytes(jwk, "y") },
            D = Bytes(jwk, "d"),
        });

    private static byte[] Bytes(JsonObject jwk, string name)
        => jwk[name]?.GetValue<string>() is { } value ? Base64Url.DecodeFromChars(value) : null;

    private sealed class TestImportSource : ISigningKeyImportSource
    {
        private readonly SigningKeyImport[] _keys;

        public TestImportSource(string keySet, params SigningKeyImport[] keys)
        {
            KeySet = keySet;
            _keys = keys;
        }

        public string KeySet { get; }

        public int Reads { get; private set; }

        public Task<IReadOnlyList<SigningKeyImport>> GetKeysAsync(CancellationToken cancellationToken = default)
        {
            Reads++;

            return Task.FromResult<IReadOnlyList<SigningKeyImport>>(_keys);
        }
    }
}
