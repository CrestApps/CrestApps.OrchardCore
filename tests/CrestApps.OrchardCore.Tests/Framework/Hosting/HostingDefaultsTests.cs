using CrestApps.Core.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Framework.Hosting;

/// <summary>
/// Pins the remaining framework host defaults.
/// </summary>
/// <remarks>
/// None of these are registered by an Orchard Core startup, so no other test in this repository
/// reaches them. They become the only implementation once the suite ships outside Orchard.
/// </remarks>
public sealed class HostingDefaultsTests
{
    [Fact]
    public void SingleTenantAccessor_AlwaysNamesTheDefaultTenant()
    {
        // Arrange & Act
        var accessor = new SingleTenantAccessor();

        // Assert: keys, groups and correlation ids are built from this, so it must never be empty.
        Assert.Equal(SingleTenantAccessor.DefaultTenantName, accessor.TenantName);
        Assert.False(string.IsNullOrWhiteSpace(accessor.TenantName));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("https://example.test", "https://example.test")]
    [InlineData("https://example.test/", "https://example.test")]
    public async Task StaticPublicBaseUrlAccessor_ReportsATrimmedUrlOrNothing(string configured, string expected)
    {
        // Arrange
        var accessor = new StaticPublicBaseUrlAccessor(configured);

        // Act
        var baseUrl = await accessor.GetBaseUrlAsync(TestContext.Current.CancellationToken);

        // Assert: callers append a path to this, so a trailing slash would produce a double slash in
        // a URL a third party is told to dial back on.
        Assert.Equal(expected, baseUrl);
    }

    [Fact]
    public async Task ScopedWorkExecutor_ResolvesTheContextFromAFreshScope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ScopeMarker>();

        var provider = services.BuildServiceProvider();
        var executor = new ServiceProviderScopedWorkExecutor(provider);

        ScopeMarker first = null;
        ScopeMarker second = null;

        // Act
        await executor.ExecuteAsync<ScopeMarker>(marker =>
        {
            first = marker;

            return Task.CompletedTask;
        });

        await executor.ExecuteAsync<ScopeMarker>(marker =>
        {
            second = marker;

            return Task.CompletedTask;
        });

        // Assert: each pass gets its own scope, or two passes would share one unit of work.
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task ScopedWorkExecutor_DrainsWorkTheOperationDeferred()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IAfterCommitTaskQueue, AfterCommitTaskQueue>();

        var provider = services.BuildServiceProvider();
        var executor = new ServiceProviderScopedWorkExecutor(provider);
        var drained = false;

        // Act
        await executor.ExecuteAsync(scoped =>
        {
            scoped.GetRequiredService<IAfterCommitTaskQueue>().Enqueue(_ =>
            {
                drained = true;

                return Task.CompletedTask;
            });

            return Task.CompletedTask;
        });

        // Assert: this scope has no outer commit to hang deferred work on, so if it did not drain
        // its own queue the work would vanish when the scope closed.
        Assert.True(drained);
    }

    [Fact]
    public async Task ScopedWorkExecutor_WithoutAnOperation_Throws()
    {
        // Arrange
        var executor = new ServiceProviderScopedWorkExecutor(new ServiceCollection().BuildServiceProvider());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => executor.ExecuteAsync((Func<IServiceProvider, Task>)null));
        await Assert.ThrowsAsync<ArgumentNullException>(() => executor.ExecuteAsync<ScopeMarker>(null));
    }

    [Fact]
    public async Task DetachedWorkExecutor_RunsTheOperationOnItsOwnScope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ScopeMarker>();

        var provider = services.BuildServiceProvider();
        var executor = new ServiceProviderDetachedWorkExecutor(provider, NullLogger<ServiceProviderDetachedWorkExecutor>.Instance);
        var completed = new TaskCompletionSource<ScopeMarker>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        executor.Run(scoped =>
        {
            completed.SetResult(scoped.GetRequiredService<ScopeMarker>());

            return Task.CompletedTask;
        });

        // Assert
        var marker = await completed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.NotNull(marker);
    }

    [Fact]
    public async Task DetachedWorkExecutor_WhenTheOperationThrows_DoesNotFaultTheProcess()
    {
        // Arrange
        var executor = new ServiceProviderDetachedWorkExecutor(
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<ServiceProviderDetachedWorkExecutor>.Instance);

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        executor.Run(_ =>
        {
            started.SetResult();

            throw new InvalidOperationException("the provider is unreachable");
        });

        await started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: nothing observes the returned task, so an unhandled exception here would surface
        // much later as an unrelated process failure. The absence of one is the assertion.
        await Task.Delay(50, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void DetachedWorkExecutor_WithoutAnOperation_Throws()
    {
        // Arrange
        var executor = new ServiceProviderDetachedWorkExecutor(
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<ServiceProviderDetachedWorkExecutor>.Instance);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => executor.Run(null));
    }

    private sealed class ScopeMarker;
}
