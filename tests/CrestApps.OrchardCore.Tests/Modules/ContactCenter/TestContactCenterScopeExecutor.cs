using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

internal sealed class TestContactCenterScopeExecutor(IServiceProvider serviceProvider) : IContactCenterScopeExecutor
{
    public bool ScheduleAfterCommitResult { get; set; }

    public Func<Task> ScheduledOperation { get; private set; }

    public Task ExecuteAsync<TContext>(Func<TContext, Task> operation)
        where TContext : notnull
    {
        return operation(serviceProvider.GetRequiredService<TContext>());
    }

    public bool ScheduleAfterCommit<TContext>(Func<TContext, Task> operation)
        where TContext : notnull
    {
        if (!ScheduleAfterCommitResult)
        {
            return false;
        }

        ScheduledOperation = () => operation(serviceProvider.GetRequiredService<TContext>());

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
