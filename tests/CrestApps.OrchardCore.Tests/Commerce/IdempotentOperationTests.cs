using CrestApps.OrchardCore.Commerce.Api;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// An operation whose idempotency key comes in a message body runs once per scope and key: a retry with the same
/// fingerprint is replayed, another fingerprint is a conflict, a second request while the first runs is told to wait,
/// and an answer that isn't final is tried afresh.
/// </summary>
public sealed class IdempotentOperationTests
{
    private static readonly TimeSpan _lifetime = TimeSpan.FromHours(1);

    private readonly IDistributedCache _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    private readonly IDistributedLock _locks = new LocalLock(NullLogger<LocalLock>.Instance);
    private int _calls;

    [Fact]
    public async Task ARetryWithTheSameFingerprint_IsReplayed()
    {
        // Arrange
        await RunAsync("scope", "key", "a", keep: true);

        // Act
        var result = await RunAsync("scope", "key", "a", keep: true);

        // Assert
        Assert.Equal(IdempotentOperationOutcome.Replayed, result.Outcome);
        Assert.Equal("answer-1", result.Value);
        Assert.Equal(1, _calls);
    }

    [Fact]
    public async Task TheSameKeyWithAnotherFingerprint_IsAConflict()
    {
        // Arrange
        await RunAsync("scope", "key", "a", keep: true);

        // Act
        var result = await RunAsync("scope", "key", "b", keep: true);

        // Assert
        Assert.Equal(IdempotentOperationOutcome.Conflict, result.Outcome);
        Assert.Null(result.Value);
        Assert.Equal(1, _calls);
    }

    [Fact]
    public async Task TheSameKeyInAnotherScope_RunsAgain()
    {
        // Arrange
        await RunAsync("scope-1", "key", "a", keep: true);

        // Act
        var result = await RunAsync("scope-2", "key", "a", keep: true);

        // Assert
        Assert.Equal(IdempotentOperationOutcome.Ran, result.Outcome);
        Assert.Equal(2, _calls);
    }

    [Fact]
    public async Task AnAnswerThatIsntFinal_IsNotKept()
    {
        // Arrange
        await RunAsync("scope", "key", "a", keep: false);

        // Act
        var result = await RunAsync("scope", "key", "a", keep: true);

        // Assert
        Assert.Equal(IdempotentOperationOutcome.Ran, result.Outcome);
        Assert.Equal("answer-2", result.Value);
    }

    [Fact]
    public async Task ASecondRequestWhileTheFirstRuns_IsInFlight()
    {
        // Arrange
        var release = new TaskCompletionSource();
        var started = new TaskCompletionSource();
        var first = IdempotentOperation.RunAsync(_cache, _locks, "scope", "key", "a", _lifetime, async _ =>
        {
            started.SetResult();
            await release.Task;

            return new IdempotentOperationAnswer("first", Keep: true);
        }, TestContext.Current.CancellationToken);

        await started.Task;

        // Act
        var second = await RunAsync("scope", "key", "a", keep: true);

        release.SetResult();
        await first;

        // Assert
        Assert.Equal(IdempotentOperationOutcome.InFlight, second.Outcome);
        Assert.Equal(0, _calls);
    }

    private Task<IdempotentOperationResult> RunAsync(string scope, string key, string fingerprint, bool keep)
        => IdempotentOperation.RunAsync(_cache, _locks, scope, key, fingerprint, _lifetime, _ =>
        {
            _calls++;

            return Task.FromResult(new IdempotentOperationAnswer($"answer-{_calls}", keep));
        }, TestContext.Current.CancellationToken);
}
