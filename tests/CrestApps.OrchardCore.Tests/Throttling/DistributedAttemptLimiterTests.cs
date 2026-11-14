using System.Net;
using System.Security.Claims;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Throttling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Throttling;

/// <summary>
/// RL-1: the attempt limiter is platform infrastructure: named policies, a window per key, failures that block a key,
/// a reset on success, fail-closed when the counter can't be locked, and a key per tenant.
/// </summary>
public sealed class DistributedAttemptLimiterTests
{
    private readonly IDistributedCache _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    // The in-memory cache expires entries by the real clock, so the test clock starts now.
    private DateTime _now = DateTime.UtcNow;

    [Fact]
    public async Task AcquireAsync_AllowsUpToTheLimit_ThenRefusesWithARetryAfter()
    {
        var limiter = Create(options => options.AddPolicy("p", permitLimit: 3, window: TimeSpan.FromMinutes(5)));

        for (var i = 0; i < 3; i++)
        {
            Assert.True((await limiter.AcquireAsync("p", "key", TestContext.Current.CancellationToken)).Allowed);
        }

        var refused = await limiter.AcquireAsync("p", "key", TestContext.Current.CancellationToken);

        Assert.False(refused.Allowed);
        Assert.True(refused.RetryAfter > TimeSpan.Zero && refused.RetryAfter <= TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task AcquireAsync_AfterTheWindow_StartsOver()
    {
        var limiter = Create(options => options.AddPolicy("p", permitLimit: 1, window: TimeSpan.FromMinutes(1)));

        Assert.True((await limiter.AcquireAsync("p", "key", TestContext.Current.CancellationToken)).Allowed);
        Assert.False((await limiter.AcquireAsync("p", "key", TestContext.Current.CancellationToken)).Allowed);

        _now = _now.AddMinutes(2);

        Assert.True((await limiter.AcquireAsync("p", "key", TestContext.Current.CancellationToken)).Allowed);
    }

    [Fact]
    public async Task RecordFailureAsync_AtTheLimit_BlocksTheKeyForTheBlockDuration()
    {
        var limiter = Create(options => options.AddPolicy("declines", permitLimit: 2, window: TimeSpan.FromMinutes(30), blockDuration: TimeSpan.FromMinutes(30)));

        await limiter.RecordFailureAsync("declines", "net", TestContext.Current.CancellationToken);
        var second = await limiter.RecordFailureAsync("declines", "net", TestContext.Current.CancellationToken);

        Assert.False(second.Allowed);
        Assert.False((await limiter.PeekAsync("declines", "net", TestContext.Current.CancellationToken)).Allowed);

        // The block outlives the window it started in.
        _now = _now.AddMinutes(29);
        Assert.False((await limiter.PeekAsync("declines", "net", TestContext.Current.CancellationToken)).Allowed);

        _now = _now.AddMinutes(2);
        Assert.True((await limiter.PeekAsync("declines", "net", TestContext.Current.CancellationToken)).Allowed);
    }

    [Fact]
    public async Task PeekAsync_DoesNotCount()
    {
        var limiter = Create(options => options.AddPolicy("p", permitLimit: 1, window: TimeSpan.FromMinutes(1)));

        await limiter.PeekAsync("p", "key", TestContext.Current.CancellationToken);
        await limiter.PeekAsync("p", "key", TestContext.Current.CancellationToken);

        Assert.True((await limiter.AcquireAsync("p", "key", TestContext.Current.CancellationToken)).Allowed);
    }

    [Fact]
    public async Task ResetAsync_ClearsTheCounter()
    {
        var limiter = Create(options => options.AddPolicy("codes", permitLimit: 1, window: TimeSpan.FromMinutes(10), resetOnSuccess: true));

        await limiter.RecordFailureAsync("codes", "net", TestContext.Current.CancellationToken);
        await limiter.ResetAsync("codes", "net", TestContext.Current.CancellationToken);

        Assert.True((await limiter.PeekAsync("codes", "net", TestContext.Current.CancellationToken)).Allowed);
    }

    [Fact]
    public async Task AnUnregisteredPolicy_Throws()
    {
        var limiter = Create(_ => { });

        await Assert.ThrowsAsync<InvalidOperationException>(() => limiter.AcquireAsync("missing", "key", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WhenTheLockCantBeTaken_Refuses()
    {
        var busy = new Mock<IDistributedLock>();
        busy.Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((Mock.Of<ILocker>(), false));

        var limiter = Create(options => options.AddPolicy("p", permitLimit: 10, window: TimeSpan.FromMinutes(1)), distributedLock: busy.Object);

        Assert.False((await limiter.AcquireAsync("p", "key", TestContext.Current.CancellationToken)).Allowed);
    }

    [Fact]
    public async Task TwoTenants_DoNotShareACounter()
    {
        var a = Create(options => options.AddPolicy("p", permitLimit: 1, window: TimeSpan.FromMinutes(1)), tenant: "A");
        var b = Create(options => options.AddPolicy("p", permitLimit: 1, window: TimeSpan.FromMinutes(1)), tenant: "B");

        Assert.True((await a.AcquireAsync("p", "key", TestContext.Current.CancellationToken)).Allowed);
        Assert.True((await b.AcquireAsync("p", "key", TestContext.Current.CancellationToken)).Allowed);
    }

    [Fact]
    public async Task ANonPositiveLimit_DisablesThePolicy()
    {
        var limiter = Create(options => options.AddPolicy("p", permitLimit: 0, window: TimeSpan.FromMinutes(1)));

        for (var i = 0; i < 20; i++)
        {
            Assert.True((await limiter.AcquireAsync("p", "key", TestContext.Current.CancellationToken)).Allowed);
        }
    }

    [Fact]
    public void Configuration_OverridesAPolicyByName()
    {
        var services = new ServiceCollection();
        services.AddAttemptLimiter();
        services.Configure<AttemptLimitOptions>(options => options.AddPolicy("payment-begin-network", 30, TimeSpan.FromMinutes(10)));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["CrestApps_Throttling:AttemptPolicies:payment-begin-network:PermitLimit"] = "5",
            })
            .Build();

        services.AddAttemptPolicyOverrides(configuration.GetSection(AttemptLimitOptions.SectionName));

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<AttemptLimitOptions>>().Value;

        Assert.Equal(5, options.Policies["payment-begin-network"].PermitLimit);
        Assert.Equal(TimeSpan.FromMinutes(10), options.Policies["payment-begin-network"].Window);
    }

    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8:1:2:3:4:5:6", "2001:db8:1:2::/64")]
    [InlineData("::ffff:198.51.100.4", "198.51.100.4")]
    public void NetworkOnly_KeysAnIPv6ClientByItsSlash64(string address, string expected)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);

        Assert.Equal(expected, ClientNetworkKey.NetworkOnly(context));
    }

    [Fact]
    public void NetworkOnly_WithoutAnAddress_IsUnknown()
    {
        Assert.Equal("unknown", ClientNetworkKey.NetworkOnly(new DefaultHttpContext()));
    }

    [Fact]
    public void From_ASignedInUser_IsTheUser_AndAGuestIsTheNetwork()
    {
        var guest = new DefaultHttpContext();
        guest.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

        var user = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "u1")], "Test")) };
        user.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

        Assert.Equal("203.0.113.7", ClientNetworkKey.From(guest));
        Assert.NotEqual(ClientNetworkKey.From(guest), ClientNetworkKey.From(user));
    }

    /// <summary>
    /// RL-1 (review): every caller now passes a network key, not an address. An IPv6 key ends with "/64", which
    /// IPAddress.TryParse refuses, so every IPv6 shopper landed in the one "unknown" bucket.
    /// </summary>
    [Theory]
    [InlineData("2001:db8:1:2:3:4:5:6")]
    [InlineData("203.0.113.7")]
    [InlineData("::ffff:198.51.100.4")]
    public void FromAddress_OfANetworkKey_GivesTheSameKeyBack(string address)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);
        var key = ClientNetworkKey.NetworkOnly(context);

        Assert.NotEqual(ClientNetworkKey.Unknown, key);
        Assert.Equal(key, ClientNetworkKey.FromAddress(key));
    }

    [Theory]
    [InlineData("203.0.113.7/64")]
    [InlineData("not-an-address/64")]
    public void FromAddress_WithASlash64ThatIsNotIPv6_IsUnknown(string value)
    {
        Assert.Equal(ClientNetworkKey.Unknown, ClientNetworkKey.FromAddress(value));
    }

    /// <summary>
    /// RL-1 minor (review): a lock miss answered with the policy's window, a day for payment-begin-checkout.
    /// </summary>
    [Fact]
    public async Task WhenTheLockCantBeTaken_TheCallerMayRetrySoon()
    {
        var busy = new Mock<IDistributedLock>();
        busy.Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((Mock.Of<ILocker>(), false));

        var limiter = Create(options => options.AddPolicy("p", permitLimit: 8, window: TimeSpan.FromHours(24)), distributedLock: busy.Object);

        var refused = await limiter.AcquireAsync("p", "key", TestContext.Current.CancellationToken);

        Assert.False(refused.Allowed);
        Assert.True(refused.RetryAfter <= TimeSpan.FromSeconds(10), $"Retry after {refused.RetryAfter}.");
    }

    /// <summary>
    /// RL-1 minor (review): a configured window of zero made every call throw (the cache refuses an expiry that isn't in
    /// the future). It is ignored, and the policy keeps its own window.
    /// </summary>
    [Fact]
    public async Task Configuration_AWindowOfZero_KeepsThePolicysWindow()
    {
        var services = new ServiceCollection();
        services.AddAttemptLimiter();
        services.Configure<AttemptLimitOptions>(options => options.AddPolicy("p", 3, TimeSpan.FromMinutes(10)));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["CrestApps_Throttling:AttemptPolicies:p:Window"] = "00:00:00",
            })
            .Build();

        services.AddAttemptPolicyOverrides(configuration.GetSection(AttemptLimitOptions.SectionName));

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<AttemptLimitOptions>>().Value;
        var limiter = Create(configure => configure.Policies["p"] = options.Policies["p"]);

        Assert.Equal(TimeSpan.FromMinutes(10), options.Policies["p"].Window);
        Assert.True((await limiter.AcquireAsync("p", "key", TestContext.Current.CancellationToken)).Allowed);
    }

    /// <summary>
    /// RL-6 (review): only Checkout and AI read the overrides, so a tenant with Scheduling or Addresses but neither
    /// ignored CrestApps_Throttling:AttemptPolicies. The limiter reads the tenant's section itself now.
    /// </summary>
    [Fact]
    public void TheTenantsOverrides_Apply_WithoutTheRegisteringModuleAskingForThem()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["CrestApps_Throttling:AttemptPolicies:booking-network:PermitLimit"] = "2",
            })
            .Build();

        var shellConfiguration = new Mock<global::OrchardCore.Environment.Shell.Configuration.IShellConfiguration>();
        shellConfiguration.Setup(value => value.GetSection(It.IsAny<string>())).Returns((string key) => configuration.GetSection(key));

        var services = new ServiceCollection();
        services.AddSingleton(shellConfiguration.Object);
        services.AddAttemptPolicy("booking-network", 20, TimeSpan.FromHours(1));

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<AttemptLimitOptions>>().Value;

        Assert.Equal(2, options.Policies["booking-network"].PermitLimit);
    }
    private DistributedAttemptLimiter Create(Action<AttemptLimitOptions> configure, IDistributedLock distributedLock = null, string tenant = "Default")
    {
        var options = new AttemptLimitOptions();
        configure(options);

        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(() => _now);

        return new DistributedAttemptLimiter(
            _cache,
            distributedLock ?? new LocalLock(NullLogger<LocalLock>.Instance),
            new ShellSettings { Name = tenant },
            clock.Object,
            Options.Create(options),
            NullLogger<DistributedAttemptLimiter>.Instance);
    }
}
