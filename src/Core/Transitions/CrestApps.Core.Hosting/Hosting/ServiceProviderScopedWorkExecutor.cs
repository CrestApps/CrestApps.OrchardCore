using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.Core.Hosting;

/// <summary>
/// The default <see cref="IScopedWorkExecutor"/>, which runs each operation in a scope created from
/// the root service provider.
/// </summary>
public sealed class ServiceProviderScopedWorkExecutor : IScopedWorkExecutor
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceProviderScopedWorkExecutor"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to create scopes.</param>
    public ServiceProviderScopedWorkExecutor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync<TContext>(Func<TContext, Task> operation)
        where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var scope = _serviceProvider.CreateAsyncScope();

        await operation(scope.ServiceProvider.GetRequiredService<TContext>());

        await DrainAsync(scope.ServiceProvider);
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(Func<IServiceProvider, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var scope = _serviceProvider.CreateAsyncScope();

        await operation(scope.ServiceProvider);

        await DrainAsync(scope.ServiceProvider);
    }

    private static async Task DrainAsync(IServiceProvider scopedServiceProvider)
    {
        // The scope this executor owns has no outer commit to hang deferred work on, so it drains
        // its own queue before the scope goes away.
        var queue = scopedServiceProvider.GetService<IAfterCommitTaskQueue>();

        if (queue is not null && queue.HasPendingWork)
        {
            await queue.DrainAsync(scopedServiceProvider);
        }
    }
}
