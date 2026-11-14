using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce;
using CrestApps.OrchardCore.AgenticCommerce.Acp;
using CrestApps.OrchardCore.AgenticCommerce.Core;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Handlers;
using CrestApps.OrchardCore.AgenticCommerce.Indexes;
using CrestApps.OrchardCore.AgenticCommerce.Migrations;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Services;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Settings;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// M13 and the agentic commerce Lows of the final review: an unregistered UCP platform signs in with a signature alone,
/// with the store's platforms in a real SQLite store.
/// </summary>
public sealed class UcpUnregisteredPlatformTests : IAsyncLifetime, IDisposable
{
    private const string RegisteredProfileUrl = "https://agent.example/.well-known/ucp";

    private static readonly DateTime _now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-ucp-platforms-{Guid.NewGuid():N}.db");
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly IDistributedCache _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    private readonly IDistributedLock _distributedLock = new LocalLock(NullLogger<LocalLock>.Instance);
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new AgentPlatformIndexProvider(), new AgentCheckoutIndexProvider()], AgenticCommerceStorage.CollectionName);
        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(AgenticCommerceStorage.CollectionName, cancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(cancellationToken);
        await new AgenticCommerceMigrations { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
        await transaction.CommitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    public void Dispose()
        => _key.Dispose();

    /// <summary>
    /// M13: a registered platform the administrator disabled, or restricted, added <c>?x</c> to its profile address. No
    /// platform has that exact address, so it came back as a fresh unregistered platform, enabled and unrestricted.
    /// </summary>
    [Fact]
    public async Task AProfileOnTheHostOfARegisteredPlatform_IsNotLetInAsAnUnregisteredPlatform()
    {
        await AddPlatformAsync(new AgentPlatform
        {
            ItemId = "platform-1",
            Name = "Agent",
            Enabled = false,
            Protocols = [AgenticCommerceConstants.Protocols.Ucp],
            ProfileUrl = RegisteredProfileUrl,
        });

        var result = await InvokeSignedAsync(RegisteredProfileUrl + "?x=1");

        Assert.Equal(StatusCodes.Status403Forbidden, StatusCode(result));
    }

    /// <summary>
    /// The control for the test above: a profile on a host no platform has still gets past its signature (and stops at the
    /// profile's unsupported version).
    /// </summary>
    [Fact]
    public async Task AProfileOnAHostNoPlatformHas_IsLetInAsAnUnregisteredPlatform()
    {
        var result = await InvokeSignedAsync("https://other.example/.well-known/ucp");

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, StatusCode(result));
    }

    /// <summary>
    /// M13: the id was a hash of the profile address as written, so one caller with one key was a new platform for each
    /// spelling (<c>?i=1</c>, <c>?i=2</c>), each with its own allowance of open checkouts.
    /// </summary>
    [Fact]
    public void AnUnregisteredPlatformsId_IsItsOrigin_NotThePathOrQueryOfItsProfile()
    {
        var id = UcpProfileClient.GetUnregisteredPlatformId("https://evil.example/p?i=1");

        Assert.Equal(id, UcpProfileClient.GetUnregisteredPlatformId("https://evil.example/p?i=2"));
        Assert.Equal(id, UcpProfileClient.GetUnregisteredPlatformId("https://EVIL.example:443/other"));
        Assert.NotEqual(id, UcpProfileClient.GetUnregisteredPlatformId("https://evil.example:8443/p"));
        Assert.NotEqual(id, UcpProfileClient.GetUnregisteredPlatformId("https://good.example/p"));
        Assert.StartsWith(AgentPlatform.UnregisteredIdPrefix, id, StringComparison.Ordinal);
    }

    /// <summary>
    /// M13: one store-wide cap for all unregistered platforms. Their open checkouts and carts all count against the one
    /// allowance, whichever of them asks.
    /// </summary>
    [Fact]
    public async Task UnregisteredPlatforms_ShareOneAllowanceOfOpenCheckouts()
    {
        await using (var session = _store.CreateSession())
        {
            var checkouts = new AgentCheckoutStore(session);

            await checkouts.CreateAsync(Checkout("c1", UcpProfileClient.GetUnregisteredPlatformId("https://one.example/p")), TestContext.Current.CancellationToken);
            await checkouts.CreateAsync(Checkout("c2", UcpProfileClient.GetUnregisteredPlatformId("https://two.example/p")), TestContext.Current.CancellationToken);
            await checkouts.CreateAsync(Checkout("c3", "platform-1"), TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var check = _store.CreateSession();
        var store = new AgentCheckoutStore(check);

        Assert.Equal(2, await store.CountOpenAsync(UcpProfileClient.GetUnregisteredPlatformId("https://three.example/p"), TestContext.Current.CancellationToken));
        Assert.Equal(1, await store.CountOpenAsync("platform-1", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// L-agentic-https-profiles: a platform's profile holds the keys its signatures are checked with, and it was accepted
    /// over plain http, where anyone on the path can swap the keys for the hour the profile is kept.
    /// </summary>
    [Fact]
    public async Task AProfileOverPlainHttp_IsRefused()
    {
        var result = await InvokeSignedAsync("http://other.example/.well-known/ucp");

        Assert.Equal(StatusCodes.Status400BadRequest, StatusCode(result));
    }

    [Fact]
    public async Task ARegisteredPlatformsProfileOverPlainHttp_IsRefusedWhenSaved()
    {
        var handler = new AgentPlatformHandler(Mock.Of<global::OrchardCore.Modules.IClock>(), new PassThroughStringLocalizer<AgentPlatformHandler>());
        var context = new global::CrestApps.Core.Models.ValidatingContext<AgentPlatform>(new AgentPlatform
        {
            Name = "Agent",
            Protocols = [AgenticCommerceConstants.Protocols.Ucp],
            ProfileUrl = "http://agent.example/.well-known/ucp",
        });

        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        Assert.Contains(context.Result.Errors, error => error.MemberNames.Contains(nameof(AgentPlatform.ProfileUrl)));
    }

    /// <summary>
    /// L-agentic-duplicate-json: a body with a property named twice failed with a 500 before the caller signed in, on the
    /// ACP MCP binding and on UCP's A2A binding.
    /// </summary>
    [Fact]
    public async Task AcpMcp_ABodyWithAPropertyNamedTwice_IsABadRequest()
    {
        var httpContext = CreateContext(CreateServices(), """{"jsonrpc":"2.0","id":1,"id":2,"method":"tools/list"}""");

        var result = await AcpMcpEndpoints.HandleAsync(httpContext, operations: null, idempotency: null);

        Assert.Equal(StatusCodes.Status400BadRequest, StatusCode(result));
    }

    [Fact]
    public async Task UcpA2a_ABodyWithAPropertyNamedTwice_IsABadRequest()
    {
        var httpContext = CreateContext(CreateServices(), """{"jsonrpc":"2.0","id":1,"id":2,"method":"message/send"}""");
        httpContext.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(UcpJsonRpc.Errors), "UcpA2a"));

        var result = await new UcpRequestFilter().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ => ValueTask.FromResult<object>(null));

        Assert.Equal(StatusCodes.Status400BadRequest, StatusCode(result));
    }

    private async Task AddPlatformAsync(AgentPlatform platform)
    {
        await using var session = _store.CreateSession();
        await new AgentPlatformStore(session).CreateAsync(platform, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static AgentCheckout Checkout(string id, string platformId)
        => new()
        {
            ItemId = id,
            PlatformId = platformId,
            Protocol = AgenticCommerceConstants.Protocols.Ucp,
            Status = AgentCheckoutStatus.Incomplete,
            ExpiresUtc = _now.AddHours(6),
        };

    // A signed GET, signed in by its signature alone, naming a profile address.
    private async Task<object> InvokeSignedAsync(string profileUrl)
    {
        await using var session = _store.CreateSession();
        var services = CreateServices(session);
        var httpContext = CreateContext(services, body: null);

        httpContext.Request.Headers[UcpConstants.AgentHeader] = $"profile=\"{profileUrl}\"";

        var signed = HttpMessageSignatures.Sign(
            UcpRequestFilter.ToSignatureMessage(httpContext.Request),
            HttpMessageSignatures.DefaultRequestComponents(hasBody: false, hasQuery: false, hasUcpAgent: true, hasIdempotencyKey: false),
            "k1",
            _key,
            created: new DateTimeOffset(_now).ToUnixTimeSeconds());

        httpContext.Request.Headers["Signature-Input"] = signed.SignatureInput;
        httpContext.Request.Headers["Signature"] = signed.Signature;

        return await new UcpRequestFilter().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ => ValueTask.FromResult<object>(null));
    }

    private ServiceProvider CreateServices(ISession session = null)
    {
        var platforms = session is null ? Mock.Of<IAgentPlatformStore>() : new AgentPlatformStore(session);
        var factory = new Mock<IHttpClientFactory>();
        var site = new SiteSettings();
        var siteService = new Mock<ISiteService>();
        var profile = new JsonObject
        {
            // A version the store doesn't serve: a request that gets past its signature stops at 422.
            ["ucp"] = new JsonObject { ["version"] = "1999-01-01" },
            ["keys"] = new JsonArray(EcJsonWebKey.FromECDsa(_key, "k1", includePrivateKey: false).ToJson()),
        };

        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(new ProfileHandler(profile)));
        site.Put(new AgenticCommerceSettings { AllowUnregisteredUcpPlatforms = true });
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site);

        return new ServiceCollection()
            .AddLogging()
            .AddSingleton(_cache)
            .AddSingleton(_distributedLock)
            .AddSingleton<AgentSignatureReplayGuard>()
            .AddSingleton(platforms)
            .AddSingleton(factory.Object)
            .AddSingleton(siteService.Object)
            .AddSingleton(Mock.Of<global::OrchardCore.Modules.IClock>(clock => clock.UtcNow == _now))
            .AddSingleton<UcpProfileCache>()
            .AddScoped<UcpProfileClient>()
            .AddSingleton(new UcpNegotiator(null))
            .AddSingleton(new AgentPlatformAuthenticator(platforms, NullLogger<AgentPlatformAuthenticator>.Instance))
            .BuildServiceProvider();
    }

    private static DefaultHttpContext CreateContext(IServiceProvider services, string body)
    {
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var bytes = body is null ? [] : Encoding.UTF8.GetBytes(body);

        httpContext.Request.Method = body is null ? HttpMethods.Get : HttpMethods.Post;
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("shop.example");
        httpContext.Request.Path = body is null ? "/ucp/checkout-sessions/cs_1" : "/ucp/a2a";
        httpContext.Request.Body = new MemoryStream(bytes);
        httpContext.Request.ContentLength = bytes.Length;

        if (body is not null)
        {
            httpContext.Request.ContentType = "application/json";
        }

        return httpContext;
    }

    private static int? StatusCode(object result)
        => Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode;

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
