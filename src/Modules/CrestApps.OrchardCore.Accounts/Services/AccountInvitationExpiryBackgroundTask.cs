using CrestApps.OrchardCore.Accounts.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Accounts.Services;

/// <summary>
/// Every hour: expires the account invitations whose time ran out, so what they held, such as a course seat, is
/// released.
/// </summary>
[BackgroundTask(
    Title = "Account invitation expiry",
    Schedule = "25 * * * *",
    Description = "Expires the account invitations nobody accepted in time.",
    LockTimeout = 3_000,
    LockExpiration = 600_000)]
internal sealed class AccountInvitationExpiryBackgroundTask : IBackgroundTask
{
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<IAccountInvitationService>().ExpireDueAsync(cancellationToken);
}
