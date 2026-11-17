using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce;
using CrestApps.OrchardCore.AgenticCommerce.Acp;
using CrestApps.OrchardCore.AgenticCommerce.Controllers;
using CrestApps.OrchardCore.AgenticCommerce.Core;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
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
    /// The registered-host Mild (fix verification): <c>Uri.IdnHost</c> keeps the trailing dot of a fully qualified name,
    /// which HttpClient fetches like the name without it, so the host check found no platform and a disabled platform came
    /// back as an unregistered one.
    /// </summary>
    [Fact]
    public async Task AProfileOnTheHostOfARegisteredPlatform_WrittenWithATrailingDot_IsNotLetInAsAnUnregisteredPlatform()
    {
        await AddPlatformAsync(new AgentPlatform
        {
            ItemId = "platform-1",
            Name = "Agent",
            Enabled = false,
            Protocols = [AgenticCommerceConstants.Protocols.Ucp],
            ProfileUrl = RegisteredProfileUrl,
        });

        var result = await InvokeSignedAsync("https://agent.example./.well-known/ucp");

        Assert.Equal(StatusCodes.Status403Forbidden, StatusCode(result));
    }

    /// <summary>
    /// The control for the tests above: a profile on a host no platform has still gets past its signature (and stops at the
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
    /// spelling (<c>?i=1</c>, <c>?i=2</c>). The query is still left out, and the origin is compared in lower case and
    /// without a trailing dot.
    /// </summary>
    [Fact]
    public void AnUnregisteredPlatformsId_IsItsProfilesOriginAndPath_NotItsQuery()
    {
        var id = UcpProfileClient.GetUnregisteredPlatformId("https://evil.example/p?i=1");

        Assert.Equal(id, UcpProfileClient.GetUnregisteredPlatformId("https://evil.example/p?i=2"));
        Assert.Equal(id, UcpProfileClient.GetUnregisteredPlatformId("https://EVIL.example:443/p"));
        Assert.Equal(id, UcpProfileClient.GetUnregisteredPlatformId("https://evil.example./p"));
        Assert.NotEqual(id, UcpProfileClient.GetUnregisteredPlatformId("https://evil.example:8443/p"));
        Assert.NotEqual(id, UcpProfileClient.GetUnregisteredPlatformId("https://good.example/p"));
        Assert.StartsWith(AgentPlatform.UnregisteredIdPrefix, id, StringComparison.Ordinal);
    }

    /// <summary>
    /// The shared-origin Mild (fix verification): the id was a hash of the origin alone, and every ownership check compares
    /// the id, so someone who could put a file on the same origin as another platform's profile was that platform, and
    /// could read, change or complete its checkouts.
    /// </summary>
    [Fact]
    public void TwoProfilesOnOneOrigin_AreTwoPlatforms()
    {
        Assert.NotEqual(
            UcpProfileClient.GetUnregisteredPlatformId("https://pages.example/alice/ucp.json"),
            UcpProfileClient.GetUnregisteredPlatformId("https://pages.example/mallory/ucp.json"));
    }

    /// <summary>
    /// MD8 (fix verification): the cap for each platform was replaced by one shared cap, so one caller with one key could
    /// open the whole allowance, and every other unregistered platform got "too many open". One origin now gets a tenth of
    /// it, whatever paths its profiles have, in addition to the shared cap.
    /// </summary>
    [Fact]
    public async Task OneUnregisteredOrigin_GetsOnlyAShareOfTheSharedAllowance()
    {
        // Twenty open in all, so one origin may have two.
        await using (var session = _store.CreateSession())
        {
            var checkouts = new AgentCheckoutStore(session);

            await checkouts.CreateAsync(UnregisteredCheckout("c1", "https://evil.example/a"), TestContext.Current.CancellationToken);
            await checkouts.CreateAsync(UnregisteredCheckout("c2", "https://evil.example/b"), TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var refused = await CreateCartAsync("https://evil.example/c", maxOpen: 20);
        var other = await CreateCartAsync("https://good.example/p", maxOpen: 20);

        Assert.Equal(StatusCodes.Status429TooManyRequests, StatusCode(refused));
        Assert.Equal(StatusCodes.Status201Created, StatusCode(other));
    }

    /// <summary>
    /// The 24-bit share Mild (fix verification): the share of one origin was counted by the first three bytes of its
    /// hash, the start of the platform id. A host whose hash starts like another's is found offline in seconds, and its
    /// checkouts then used up the other origin's share. The share is counted by a 16-byte hash in its own column now.
    /// </summary>
    [Fact]
    public async Task TwoOriginsWhoseIdsStartAlike_EachHaveTheirOwnShare()
    {
        var (first, second) = FindOriginsWhoseIdsStartAlike();

        await using (var session = _store.CreateSession())
        {
            var checkouts = new AgentCheckoutStore(session);

            await checkouts.CreateAsync(UnregisteredCheckout("c1", first + "/a"), TestContext.Current.CancellationToken);
            await checkouts.CreateAsync(UnregisteredCheckout("c2", first + "/b"), TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var refused = await CreateCartAsync(first + "/c", maxOpen: 20);
        var other = await CreateCartAsync(second + "/p", maxOpen: 20);

        Assert.Equal(StatusCodes.Status429TooManyRequests, StatusCode(refused));
        Assert.Equal(StatusCodes.Status201Created, StatusCode(other));
    }

    /// <summary>
    /// The registered-host Mild (fix verification): the host search narrowed the platforms in SQL by the lower-case
    /// punycode host, so a profile address saved in Unicode (or, on PostgreSQL, with capitals) was never a candidate, and a
    /// disabled platform came back as an unregistered one from its own host.
    /// </summary>
    [Theory]
    [InlineData("https://bücher.example/.well-known/ucp", "https://xn--bcher-kva.example/.well-known/ucp?x=1")]
    [InlineData("https://Agent.Example/.well-known/ucp", "https://agent.example/.well-known/ucp?x=1")]
    public async Task AProfileOnTheHostOfARegisteredPlatform_SavedInAnotherForm_IsNotLetInAsAnUnregisteredPlatform(string registered, string signedWith)
    {
        await AddPlatformAsync(new AgentPlatform
        {
            ItemId = "platform-1",
            Name = "Agent",
            Enabled = false,
            Protocols = [AgenticCommerceConstants.Protocols.Ucp],
            ProfileUrl = registered,
        });

        var result = await InvokeSignedAsync(signedWith);

        Assert.Equal(StatusCodes.Status403Forbidden, StatusCode(result));
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

    /// <summary>
    /// The duplicate-property Mild (fix verification): the embedded checkout's credential check parsed its body with the
    /// default options, so a body with <c>credential</c> named twice threw past its JsonException handler, a 500.
    /// </summary>
    [Fact]
    public void EmbeddedCheckout_ACredentialNamedTwice_IsNoCredential()
    {
        var readCredential = typeof(EmbeddedCheckoutController).GetMethod("ReadCredential", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.Null(readCredential.Invoke(null, ["""{"credential":"a","credential":"b"}"""]));
        Assert.Equal("a", readCredential.Invoke(null, ["""{"credential":"a"}"""]));
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

    // A checkout of the unregistered platform of a profile address, with the address the request filter keeps on it.
    private static AgentCheckout UnregisteredCheckout(string id, string profileUrl)
    {
        var checkout = Checkout(id, UcpProfileClient.GetUnregisteredPlatformId(profileUrl));

        checkout.SetProperty(UcpProfileClient.ProfileUrlProperty, profileUrl);

        return checkout;
    }

    // Two https origins whose unregistered platform ids start with the same characters: the prefix and three bytes of a
    // hash, so about four thousand hosts find a pair.
    private static (string First, string Second) FindOriginsWhoseIdsStartAlike()
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var i = 0; ; i++)
        {
            var origin = $"https://h{i}.example";
            var start = UcpProfileClient.GetUnregisteredPlatformId(origin + "/p")[..AgentPlatform.UnregisteredOriginKeyLength];

            if (seen.TryGetValue(start, out var first))
            {
                return (first, origin);
            }

            seen[start] = origin;
        }
    }

    // Creates a cart through the UCP endpoint, as the unregistered platform of a profile address, on the real store.
    private async Task<object> CreateCartAsync(string profileUrl, int maxOpen)
    {
        await using var session = _store.CreateSession();
        var site = new SiteSettings();
        var siteService = new Mock<ISiteService>();
        var checkouts = new Mock<IAgentCheckoutService>();
        var platformId = UcpProfileClient.GetUnregisteredPlatformId(profileUrl);

        site.Put(new AgenticCommerceSettings { AllowUnregisteredUcpPlatforms = true, MaxOpenCheckoutsPerPlatform = maxOpen });
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site);
        checkouts
            .Setup(service => service.PreviewAsync(It.IsAny<AgentCheckoutContext>(), It.IsAny<IList<AgentCheckoutLineInput>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentCheckoutView { Currency = "usd" });

        var httpContext = new DefaultHttpContext();

        new AgentRequest
        {
            Platform = new AgentPlatform { ItemId = platformId, ProfileUrl = profileUrl },
            Version = "2026-08-25",
            Json = JsonNode.Parse("""{ "line_items": [ { "item": { "id": "mug" }, "quantity": 1 } ] }"""),
            Context = new AgentCheckoutContext
            {
                PlatformId = platformId,
                Properties = new Dictionary<string, string> { [UcpProfileClient.ProfileUrlProperty] = profileUrl },
            },
        }.Set(httpContext);

        var method = typeof(UcpCatalogEndpoints).GetMethod("CreateCartAsync", BindingFlags.NonPublic | BindingFlags.Static);
        var task = (Task)method.Invoke(null, [httpContext, checkouts.Object, new AgentCheckoutStore(session), siteService.Object, Mock.Of<global::OrchardCore.Modules.IClock>(clock => clock.UtcNow == _now)]);

        await task;
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return task.GetType().GetProperty("Result").GetValue(task);
    }

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
