using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Throttling;
using CrestApps.OrchardCore.Users.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Customers;

/// <summary>
/// A code emailed for signing in works once, for ten minutes and five tries, for the email and tenant it was made
/// for; and sending them is limited per email and per address.
/// </summary>
public sealed class EmailSignInCodesTests
{
    private readonly IDistributedCache _cache = new SlowCache(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
    private readonly IDistributedLock _lock = new LocalLock(NullLogger<LocalLock>.Instance);
    private DateTime _now = DateTime.UtcNow;

    [Fact]
    public async Task TheRightCodeWorksOnce()
    {
        var codes = Create();
        var code = await codes.IssueAsync("jordan@example.test", TestContext.Current.CancellationToken);

        Assert.Matches("^[0-9]{6}$", code);
        Assert.True(await codes.VerifyAsync(" Jordan@Example.TEST ", code, TestContext.Current.CancellationToken));
        Assert.False(await codes.VerifyAsync("jordan@example.test", code, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SpacesInTheTypedCodeAreIgnored()
    {
        var codes = Create();
        var code = await codes.IssueAsync("jordan@example.test", TestContext.Current.CancellationToken);

        Assert.True(await codes.VerifyAsync("jordan@example.test", code[..3] + " " + code[3..], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ACodeIsForItsEmailAndTenantOnly()
    {
        var code = await Create("tenant-a").IssueAsync("jordan@example.test", TestContext.Current.CancellationToken);

        Assert.False(await Create("tenant-a").VerifyAsync("someone@example.test", code, TestContext.Current.CancellationToken));
        Assert.False(await Create("tenant-b").VerifyAsync("jordan@example.test", code, TestContext.Current.CancellationToken));
        Assert.True(await Create("tenant-a").VerifyAsync("jordan@example.test", code, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FiveWrongTriesUseTheCodeUp()
    {
        var codes = Create();
        var code = await codes.IssueAsync("jordan@example.test", TestContext.Current.CancellationToken);
        var wrong = code == "000000" ? "111111" : "000000";

        for (var i = 0; i < EmailSignInCodes.MaxAttempts; i++)
        {
            Assert.False(await codes.VerifyAsync("jordan@example.test", wrong, TestContext.Current.CancellationToken));
        }

        Assert.False(await codes.VerifyAsync("jordan@example.test", code, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ACodeExpiresAfterTenMinutes()
    {
        var codes = Create();
        var code = await codes.IssueAsync("jordan@example.test", TestContext.Current.CancellationToken);

        _now = _now.AddMinutes(11);

        Assert.False(await codes.VerifyAsync("jordan@example.test", code, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ANewCodeReplacesTheLastOne()
    {
        var codes = Create();
        var first = await codes.IssueAsync("jordan@example.test", TestContext.Current.CancellationToken);
        var second = await codes.IssueAsync("jordan@example.test", TestContext.Current.CancellationToken);

        if (first != second)
        {
            Assert.False(await codes.VerifyAsync("jordan@example.test", first, TestContext.Current.CancellationToken));
        }

        Assert.True(await codes.VerifyAsync("jordan@example.test", second, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendingIsLimitedPerEmail_ThenAllowedAgainLater()
    {
        var codes = Create();

        for (var i = 0; i < 3; i++)
        {
            Assert.True(await codes.TryCountSendAsync("jordan@example.test", "10.0.0." + i, TestContext.Current.CancellationToken));
        }

        Assert.False(await codes.TryCountSendAsync("JORDAN@example.test", "10.0.0.9", TestContext.Current.CancellationToken));

        _now = _now.AddMinutes(16);

        Assert.True(await codes.TryCountSendAsync("jordan@example.test", "10.0.0.9", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendingIsLimitedPerAddress()
    {
        var codes = Create();

        for (var i = 0; i < 10; i++)
        {
            Assert.True(await codes.TryCountSendAsync($"buyer{i}@example.test", "10.0.0.1", TestContext.Current.CancellationToken));
        }

        Assert.False(await codes.TryCountSendAsync("another@example.test", "10.0.0.1", TestContext.Current.CancellationToken));
        Assert.True(await codes.TryCountSendAsync("another@example.test", "10.0.0.2", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// SEC-P1: the attempt count was read, compared and written with no lock, so parallel guesses all saw fewer than five
    /// attempts and a six-digit code could be brute-forced. A hundred parallel wrong guesses now use the code up.
    /// </summary>
    [Fact]
    public async Task ParallelWrongGuesses_UseTheCodeUp()
    {
        var codes = Create();
        var code = await codes.IssueAsync("jordan@example.test", TestContext.Current.CancellationToken);
        var wrong = code == "000000" ? "111111" : "000000";

        var results = await Task.WhenAll(Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() => codes.VerifyAsync("jordan@example.test", wrong, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken)));

        Assert.DoesNotContain(true, results);
        Assert.False(await codes.VerifyAsync("jordan@example.test", code, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// SEC-P1: parallel sends can't get past the per-email limit either.
    /// </summary>
    [Fact]
    public async Task ParallelSends_StayWithinTheLimit()
    {
        var codes = Create();

        var results = await Task.WhenAll(Enumerable.Range(0, 30)
            .Select(i => Task.Run(() => codes.TryCountSendAsync("jordan@example.test", "10.0.1." + i, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken)));

        Assert.Equal(3, results.Count(allowed => allowed));
    }

    /// <summary>
    /// SEC-P1: when the lock can't be taken, the guess is refused rather than counted without it.
    /// </summary>
    [Fact]
    public async Task WhenTheLockCantBeTaken_TheGuessIsRefused()
    {
        var code = await Create().IssueAsync("jordan@example.test", TestContext.Current.CancellationToken);

        var busy = new Mock<IDistributedLock>();
        busy.Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((Mock.Of<ILocker>(), false));

        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(() => _now);
        var codes = new EmailSignInCodes(_cache, CreateLimiter(clock.Object), new ShellSettings { Name = "Default" }, clock.Object, busy.Object);

        Assert.False(await codes.VerifyAsync("jordan@example.test", code, TestContext.Current.CancellationToken));
    }

    private EmailSignInCodes Create(string tenant = "Default")
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(() => _now);

        return new EmailSignInCodes(_cache, CreateLimiter(clock.Object), new ShellSettings { Name = tenant }, clock.Object, _lock);
    }

    // The shared attempt limiter with the feature's default policies.
    private DistributedAttemptLimiter CreateLimiter(IClock clock)
    {
        var options = new AttemptLimitOptions()
            .AddPolicy(EmailSignInCodes.SendNetworkPolicy, 10, TimeSpan.FromMinutes(15))
            .AddPolicy(EmailSignInCodes.SendEmailPolicy, 3, TimeSpan.FromMinutes(15))
            .AddPolicy(EmailSignInCodes.VerifyNetworkPolicy, 30, TimeSpan.FromMinutes(15));

        return new DistributedAttemptLimiter(_cache, _lock, new ShellSettings { Name = "Default" }, clock, Options.Create(options), NullLogger<DistributedAttemptLimiter>.Instance);
    }

    [Fact]
    public async Task NoCode_IsToldApartFromAWrongCode()
    {
        var codes = Create();

        Assert.Equal(EmailSignInCodeCheck.NoCode, await codes.CheckAsync("jordan@example.test", "123456", TestContext.Current.CancellationToken));

        var code = await codes.IssueAsync("jordan@example.test", TestContext.Current.CancellationToken);
        var wrong = code == "000000" ? "111111" : "000000";

        Assert.Equal(EmailSignInCodeCheck.Wrong, await codes.CheckAsync("jordan@example.test", wrong, TestContext.Current.CancellationToken));
        Assert.Equal(EmailSignInCodeCheck.Right, await codes.CheckAsync("jordan@example.test", code, TestContext.Current.CancellationToken));
        Assert.Equal(EmailSignInCodeCheck.NoCode, await codes.CheckAsync("jordan@example.test", code, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheCacheKeys_HoldNoEmail()
    {
        var keys = new List<string>();
        var cache = new Mock<IDistributedCache>();
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((key, _, _, _) => keys.Add(key))
            .Returns(Task.CompletedTask);
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(() => _now);
        var codes = new EmailSignInCodes(cache.Object, CreateLimiter(clock.Object), new ShellSettings { Name = "Default" }, clock.Object, _lock);

        await codes.IssueAsync("jordan@example.test", TestContext.Current.CancellationToken);

        Assert.NotEmpty(keys);
        Assert.DoesNotContain(keys, key => key.Contains("JORDAN", StringComparison.OrdinalIgnoreCase));
    }

    // A cache that yields on every read, so concurrent callers interleave between the read and the write the way they do
    // against a real distributed cache.
    private sealed class SlowCache : IDistributedCache
    {
        private readonly IDistributedCache _inner;

        public SlowCache(IDistributedCache inner)
            => _inner = inner;

        public byte[] Get(string key) => _inner.Get(key);

        public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            await Task.Delay(2, token);

            return await _inner.GetAsync(key, token);
        }

        public void Refresh(string key) => _inner.Refresh(key);

        public Task RefreshAsync(string key, CancellationToken token = default) => _inner.RefreshAsync(key, token);

        public void Remove(string key) => _inner.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default) => _inner.RemoveAsync(key, token);

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _inner.Set(key, value, options);

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
            => _inner.SetAsync(key, value, options, token);
    }
}
