using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

internal sealed class TestContactCenterScopeExecutor : IContactCenterScopeExecutor
{
    private readonly IServiceProvider _serviceProvider;

    public TestContactCenterScopeExecutor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public bool ScheduleAfterCommitResult { get; set; }

    public Func<Task> ScheduledOperation { get; private set; }

    public Task ExecuteAsync<TContext>(Func<TContext, Task> operation)
        where TContext : notnull
    {
        return operation(_serviceProvider.GetRequiredService<TContext>());
    }

    public bool ScheduleAfterCommit<TContext>(Func<TContext, Task> operation)
        where TContext : notnull
    {
        if (!ScheduleAfterCommitResult)
        {
            return false;
        }

        ScheduledOperation = () => operation(_serviceProvider.GetRequiredService<TContext>());

        return true;
    }

    public bool ScheduleAfterCommit(Func<Task> operation)
    {
        if (!ScheduleAfterCommitResult)
        {
            return false;
        }

        ScheduledOperation = operation;

        return true;
    }
}
