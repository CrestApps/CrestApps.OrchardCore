using CrestApps.OrchardCore.Memberships.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Memberships.BackgroundTasks;

/// <summary>
/// Every hour: ends the memberships whose end date passed, taking their members out of the roles.
/// </summary>
[BackgroundTask(
    Title = "Membership end dates",
    Schedule = "15 * * * *",
    Description = "Ends memberships whose end date passed, and takes their members out of the roles.",
    LockTimeout = 3_000,
    LockExpiration = 300_000)]
public sealed class MembershipsBackgroundTask : IBackgroundTask
{
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<MembershipService>().ExpireAsync(200, cancellationToken);
}
