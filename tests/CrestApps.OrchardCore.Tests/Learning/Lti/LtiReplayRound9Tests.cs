using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Lti;
using CrestApps.OrchardCore.Learning.Lti.Core.DeepLinking;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Learning.Lti;

/// <summary>
/// Review round 9, M5: replay protection lasts as long as what it protects. A client assertion needs <c>iat</c> and a
/// short lifetime; a Deep Linking response cannot be answered again while its data is valid; two requests with the same
/// value at once let only one through.
/// </summary>
public sealed class LtiReplayRound9Tests
{
    private const string Issuer = "https://lms.example.com";
    private const string TokenEndpoint = "https://lms.example.com/connect/token";

    private static readonly DateTime _now = new(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);
    private static readonly RSA _toolKey = RSA.Create(2048);

    private readonly TestClock _clock = new(_now);
    private readonly ExternalTool _tool = new() { ItemId = "tool-1", ClientId = "client-1", DeploymentId = "deployment-1", Enabled = true, SubjectSalt = LtiSubjects.NewSalt() };

    [Fact]
    public async Task Round9M5_AClientAssertion_NeedsIat_AndALifetimeOfAFewMinutes()
    {
        // Arrange
        var cache = new ClockCache(_clock);

        // Act
        var longLived = await ValidateAssertionAsync(cache, Assertion(issuedAt: _now, expires: _now.AddHours(1)));
        var withoutIat = await ValidateAssertionAsync(cache, Assertion(issuedAt: null, expires: _now.AddMinutes(5)));
        var shortLived = await ValidateAssertionAsync(cache, Assertion(issuedAt: _now, expires: _now.AddMinutes(5)));

        // Assert
        Assert.True(longLived.IsRejected, "An assertion valid for an hour was accepted.");
        Assert.True(withoutIat.IsRejected, "An assertion without iat was accepted.");
        Assert.False(shortLived.IsRejected, shortLived.ErrorDescription);
    }

    [Fact]
    public async Task Round9M5_ADeepLinkingResponse_CannotBeAnsweredAgain_WhileItsDataIsValid()
    {
        // Arrange: the platform's Deep Linking data is valid for an hour.
        var cache = new ClockCache(_clock);
        var protection = new EphemeralDataProtectionProvider();
        var data = new LtiDeepLinkingDataTokens(protection, _clock).Create(new LtiDeepLinkingData { ToolId = "tool-1", UserId = "user-1", CourseId = "course-1" });

        // Act: answered once; twenty minutes later the tool signs a new response with the same data.
        var first = await DeepLinkingService(cache, protection).ReadResponseAsync(DeepLinkingResponse(data), TestContext.Current.CancellationToken);

        _clock.UtcNow = _now.AddMinutes(20);

        var replayed = await DeepLinkingService(cache, protection).ReadResponseAsync(DeepLinkingResponse(data), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first.Succeeded, first.Error);
        Assert.False(replayed.Succeeded, "The same Deep Linking data was answered twice.");
    }

    [Fact]
    public async Task Round9M5_TheSameValueTwiceAtOnce_LetsOnlyOneThrough()
    {
        // Arrange: a cache slow enough that both requests read before either writes.
        var nonces = Store(new SlowCache(new ClockCache(_clock)));

        // Act
        var results = await Task.WhenAll(
            nonces.TryUseAsync("client-assertion:client-1", "jti-1", TestContext.Current.CancellationToken),
            nonces.TryUseAsync("client-assertion:client-1", "jti-1", TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(1, results.Count(result => result));
    }

    private LtiNonceStore Store(IDistributedCache cache)
    {
        var services = new ServiceCollection()
            .AddSingleton(cache)
            .AddSingleton<IDistributedLock>(new KeyedLock())
            .AddSingleton<IClock>(_clock)
            .BuildServiceProvider();

        return ActivatorUtilities.CreateInstance<LtiNonceStore>(services);
    }

    private LtiDeepLinkingService DeepLinkingService(IDistributedCache cache, IDataProtectionProvider protection)
        => new(new Tools(_tool), new Platform(), Store(cache), new LtiDeepLinkingDataTokens(protection, _clock), _clock, NullLogger<LtiDeepLinkingService>.Instance);

    private async Task<OpenIddictServerEvents.ValidateTokenContext> ValidateAssertionAsync(IDistributedCache cache, string assertion)
    {
        var context = new OpenIddictServerEvents.ValidateTokenContext(new OpenIddictServerTransaction { Logger = NullLogger.Instance })
        {
            Token = assertion,
        };

        context.ValidTokenTypes.Add("urn:openiddict:params:oauth:token-type:client_assertion");

        await new LtiClientAssertionHandler(new Tools(_tool), new Platform(), Store(cache), _clock, NullLogger<LtiClientAssertionHandler>.Instance).HandleAsync(context);

        return context;
    }

    private static string Assertion(DateTime? issuedAt, DateTime expires)
    {
        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = "client-1",
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("n"),
        };

        var handler = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false };

        return handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "client-1",
            Audience = TokenEndpoint,
            IssuedAt = issuedAt,
            Expires = expires,
            Claims = claims,
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(_toolKey) { KeyId = "tool-key-1" }, SecurityAlgorithms.RsaSha256),
        });
    }

    private string DeepLinkingResponse(string data)
    {
        var now = _clock.UtcNow;

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "client-1",
            Audience = Issuer,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(5),
            Claims = new Dictionary<string, object>
            {
                [LtiConstants.Claims.MessageType] = LtiConstants.MessageTypes.DeepLinkingResponse,
                [LtiConstants.Claims.Version] = "1.3.0",
                [LtiConstants.Claims.DeploymentId] = "deployment-1",
                ["nonce"] = Guid.NewGuid().ToString("n"),
                [LtiConstants.Claims.Data] = data,
                [LtiConstants.Claims.ContentItems] = System.Text.Json.JsonSerializer.SerializeToElement(new JsonArray(new JsonObject { ["type"] = "ltiResourceLink", ["title"] = "Simulator" })),
            },
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(_toolKey) { KeyId = "tool-key-1" }, SecurityAlgorithms.RsaSha256),
        });
    }

    // A distributed cache whose entries expire by the test clock.
    private sealed class ClockCache : IDistributedCache
    {
        private readonly ConcurrentDictionary<string, (byte[] Value, DateTimeOffset? Expires)> _entries = new(StringComparer.Ordinal);
        private readonly TestClock _clock;

        public ClockCache(TestClock clock)
        {
            _clock = clock;
        }

        public byte[] Get(string key)
            => _entries.TryGetValue(key, out var entry) && (entry.Expires is null || entry.Expires > _clock.UtcNow) ? entry.Value : null;

        public Task<byte[]> GetAsync(string key, CancellationToken token = default)
            => Task.FromResult(Get(key));

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
            => _entries[key] = (value, options.AbsoluteExpiration ?? (options.AbsoluteExpirationRelativeToNow is { } relative ? new DateTimeOffset(_clock.UtcNow) + relative : null));

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
            => _entries.TryRemove(key, out _);

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);

            return Task.CompletedTask;
        }
    }

    private sealed class SlowCache : IDistributedCache
    {
        private readonly IDistributedCache _inner;

        public SlowCache(IDistributedCache inner)
        {
            _inner = inner;
        }

        public byte[] Get(string key) => _inner.Get(key);

        public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            var value = await _inner.GetAsync(key, token);
            await Task.Delay(100, token);

            return value;
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _inner.Set(key, value, options);

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => _inner.SetAsync(key, value, options, token);

        public void Refresh(string key) => _inner.Refresh(key);

        public Task RefreshAsync(string key, CancellationToken token = default) => _inner.RefreshAsync(key, token);

        public void Remove(string key) => _inner.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default) => _inner.RemoveAsync(key, token);
    }

    private sealed class Tools : ILtiToolDirectory
    {
        private readonly ExternalTool _tool;

        public Tools(ExternalTool tool)
        {
            _tool = tool;
        }

        public Task<ExternalTool> FindByClientIdAsync(string clientId, CancellationToken cancellationToken = default)
            => Task.FromResult(clientId == _tool.ClientId ? _tool : null);

        public Task<ExternalTool> FindByIdAsync(string toolId, CancellationToken cancellationToken = default)
            => Task.FromResult(toolId == _tool.ItemId ? _tool : null);

        public Task<bool> IsRedirectUriAllowedAsync(string clientId, string redirectUri, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(ExternalTool tool, bool refresh = false, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SecurityKey>>([new RsaSecurityKey(_toolKey.ExportParameters(false)) { KeyId = "tool-key-1" }]);
    }

    private sealed class Platform : ILtiPlatform
    {
        public Task<LtiPlatformInfo> GetAsync()
            => Task.FromResult(new LtiPlatformInfo { Issuer = Issuer, TokenEndpoint = TokenEndpoint });
    }
}
