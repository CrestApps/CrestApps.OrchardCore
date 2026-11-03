using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Executes Contact Center operations through Orchard shell scopes.
/// </summary>
public sealed class ContactCenterScopeExecutor : IContactCenterScopeExecutor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IShellHost _shellHost;
    private readonly ShellSettings _shellSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterScopeExecutor"/> class.
    /// </summary>
    /// <param name="serviceProvider">The current shell service provider.</param>
    public ContactCenterScopeExecutor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        // Taken now, while the scope this executor came from is alive: work that outlives that scope (a provider command
        // its timeout gave up waiting on, which keeps running) finds it disposed, and no shell scope current any more.
        _shellHost = serviceProvider.GetService<IShellHost>();
        _shellSettings = serviceProvider.GetService<ShellSettings>();
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync<TContext>(Func<TContext, Task> operation)
        where TContext : notnull
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (ShellScope.Current is null)
        {
            await ExecuteOutsideShellScopeAsync(services => operation(services.GetRequiredService<TContext>()));

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
            await ExecuteOutsideShellScopeAsync(operation);

            return;
        }

        await ShellScope.UsingChildScopeAsync(scope => operation(scope.ServiceProvider));
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

    // With no shell scope current -- never started, or ended under work still running -- the work gets a scope of the
    // tenant's own, which belongs to the shell rather than to whatever scope this executor was resolved from.
    private async Task ExecuteOutsideShellScopeAsync(Func<IServiceProvider, Task> operation)
    {
        if (_shellHost is not null && _shellSettings is not null)
        {
            var shellScope = await _shellHost.GetScopeAsync(_shellSettings);

            await shellScope.UsingAsync(scope => operation(scope.ServiceProvider));

            return;
        }

        await using var scope = _serviceProvider.CreateAsyncScope();
        await operation(scope.ServiceProvider);
    }
}
