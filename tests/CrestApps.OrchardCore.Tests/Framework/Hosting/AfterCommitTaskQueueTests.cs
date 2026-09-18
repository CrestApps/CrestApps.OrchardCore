using CrestApps.Core.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Framework.Hosting;

/// <summary>
/// Pins the framework's default after-commit queue.
/// </summary>
/// <remarks>
/// No Orchard Core startup registers this — Orchard hangs the same work on its shell scope instead.
/// The suite defers provider calls until after a commit so a caller is never told about a call the
/// database has not recorded, which makes "the queue dropped an entry" a lost notification rather
/// than a visible error.
/// </remarks>
public sealed class AfterCommitTaskQueueTests
{
    [Fact]
    public void ANewQueue_HasNothingPending()
    {
        // Arrange & Act
        var queue = new AfterCommitTaskQueue();

        // Assert
        Assert.False(queue.HasPendingWork);
    }

    [Fact]
    public void Enqueue_WithoutAnOperation_Throws()
    {
        // Arrange
        var queue = new AfterCommitTaskQueue();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => queue.Enqueue(null));
    }

    [Fact]
    public async Task DrainAsync_RunsOperationsInTheOrderTheyWereEnqueued()
    {
        // Arrange
        var queue = new AfterCommitTaskQueue();
        var order = new List<string>();

        queue.Enqueue(_ =>
        {
            order.Add("first");

            return Task.CompletedTask;
        });
        queue.Enqueue(_ =>
        {
            order.Add("second");

            return Task.CompletedTask;
        });

        // Act
        await queue.DrainAsync(EmptyProvider(), TestContext.Current.CancellationToken);

        // Assert: ordering matters, because a hangup queued after a transfer must not run first.
        Assert.Equal(["first", "second"], order);
        Assert.False(queue.HasPendingWork);
    }

    [Fact]
    public async Task DrainAsync_RunsWorkThatAnOperationItselfEnqueued()
    {
        // Arrange
        var queue = new AfterCommitTaskQueue();
        var ran = new List<string>();

        queue.Enqueue(_ =>
        {
            ran.Add("outer");
            queue.Enqueue(_ =>
            {
                ran.Add("inner");

                return Task.CompletedTask;
            });

            return Task.CompletedTask;
        });

        // Act
        await queue.DrainAsync(EmptyProvider(), TestContext.Current.CancellationToken);

        // Assert: iterating a snapshot would drop the nested entry and report success.
        Assert.Equal(["outer", "inner"], ran);
        Assert.False(queue.HasPendingWork);
    }

    [Fact]
    public async Task DrainAsync_HandsEachOperationTheServiceProvider()
    {
        // Arrange
        var queue = new AfterCommitTaskQueue();
        var provider = EmptyProvider();
        IServiceProvider observed = null;

        queue.Enqueue(scoped =>
        {
            observed = scoped;

            return Task.CompletedTask;
        });

        // Act
        await queue.DrainAsync(provider, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(provider, observed);
    }

    [Fact]
    public async Task DrainAsync_WhenAnOperationThrows_Propagates()
    {
        // Arrange
        var queue = new AfterCommitTaskQueue();
        queue.Enqueue(_ => throw new InvalidOperationException("the provider rejected the command"));

        // Act & Assert: the owner of the commit decides what a failure means, so it is not swallowed here.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => queue.DrainAsync(EmptyProvider(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DrainAsync_WithoutAServiceProvider_Throws()
    {
        // Arrange
        var queue = new AfterCommitTaskQueue();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => queue.DrainAsync(null, TestContext.Current.CancellationToken));
    }

    private static ServiceProvider EmptyProvider()
        => new ServiceCollection().BuildServiceProvider();
}
