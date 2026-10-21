using CrestApps.OrchardCore.Users.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Customers;

/// <summary>
/// A code emailed for signing in works once, for ten minutes and five tries, for the email and tenant it was made
/// for; and sending them is limited per email and per address.
/// </summary>
public sealed class EmailSignInCodesTests
{
    private readonly IDistributedCache _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
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

    private EmailSignInCodes Create(string tenant = "Default")
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(() => _now);

        return new EmailSignInCodes(_cache, new ShellSettings { Name = tenant }, clock.Object);
    }
}
