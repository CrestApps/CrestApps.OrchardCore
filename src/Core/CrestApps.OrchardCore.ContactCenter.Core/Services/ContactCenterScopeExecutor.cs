using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Executes Contact Center operations through Orchard shell scopes.
/// </summary>
public sealed class ContactCenterScopeExecutor : IContactCenterScopeExecutor
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterScopeExecutor"/> class.
    /// </summary>
    /// <param name="serviceProvider">The current shell service provider.</param>
    public ContactCenterScopeExecutor(IServiceProvider serviceProvider)
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
    public bool ScheduleAfterCommit<TContext>(Func<TContext, Task> operation)
        where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (ShellScope.Current is null)
        {
            return false;
        }

        ShellScope.AddDeferredTask(scope =>
            operation(scope.ServiceProvider.GetRequiredService<TContext>()));

        return true;
    }

    /// <inheritdoc/>
    public bool ScheduleAfterCommit(Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (ShellScope.Current is null)
        {
            return false;
        }

        ShellScope.AddDeferredTask(_ => operation());

        return true;
    }
}
