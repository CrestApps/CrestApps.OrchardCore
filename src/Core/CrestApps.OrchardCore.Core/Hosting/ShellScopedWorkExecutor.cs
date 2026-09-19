using CrestApps.Core.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Core.Hosting;

/// <summary>
/// Runs work in an Orchard child shell scope rather than a bare dependency-injection scope.
/// </summary>
/// <remarks>
/// <para>
/// The difference matters. A child shell scope is a unit of work: what the operation writes is
/// committed when the scope ends, and anything deferred until commit runs then. A bare scope only
/// bounds the lifetime of the services, so the same operation would appear to succeed and persist
/// nothing.
/// </para>
/// <para>
/// When there is no ambient shell scope - which is how the framework default is reached in a host with
/// no notion of tenancy, and how a unit test reaches it - this falls back to a plain scope, because a
/// child of nothing is not something Orchard can make.
/// </para>
/// </remarks>
public sealed class ShellScopedWorkExecutor : IScopedWorkExecutor
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellScopedWorkExecutor"/> class.
    /// </summary>
    /// <param name="serviceProvider">The current shell service provider.</param>
    public ShellScopedWorkExecutor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync<TContext>(Func<TContext, Task> operation)
        where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (ShellScope.Current is null)
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            await operation(scope.ServiceProvider.GetRequiredService<TContext>());

            return;
        }

        await ShellScope.UsingChildScopeAsync(scope =>
            operation(scope.ServiceProvider.GetRequiredService<TContext>()));
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(Func<IServiceProvider, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (ShellScope.Current is null)
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            await operation(scope.ServiceProvider);

            return;
        }

        await ShellScope.UsingChildScopeAsync(scope => operation(scope.ServiceProvider));
    }
}
