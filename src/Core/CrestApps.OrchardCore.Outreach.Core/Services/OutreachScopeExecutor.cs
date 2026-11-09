using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Outreach.Core.Services;

/// <summary>
/// Runs work in a child of the current shell scope, or, outside a shell (as in tests), in a new service scope.
/// </summary>
public sealed class OutreachScopeExecutor : IOutreachScopeExecutor
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutreachScopeExecutor"/> class.
    /// </summary>
    /// <param name="serviceScopeFactory">The factory used outside a shell scope.</param>
    public OutreachScopeExecutor(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(Func<IServiceProvider, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (ShellScope.Current is not null)
        {
            await ShellScope.UsingChildScopeAsync(scope => operation(scope.ServiceProvider));

            return;
        }

        await using var scope = _serviceScopeFactory.CreateAsyncScope();

        await operation(scope.ServiceProvider);
    }
}
