using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Notifications;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Sends Learning notices through <see cref="INotificationService"/> in a deferred task of the shell scope, which runs
/// once the request's changes are committed.
/// </summary>
public sealed class LearningNotificationSender : ILearningNotificationSender
{
    /// <inheritdoc />
    public void Send(string userId, LearningNotificationMessage message)
    {
        if (string.IsNullOrEmpty(userId) || message is null)
        {
            return;
        }

        ShellScope.AddDeferredTask(async scope =>
        {
            var services = scope.ServiceProvider;

            if (services.GetService<INotificationService>() is not { } notifications)
            {
                return;
            }

            var user = await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync(userId);

            if (user is not null)
            {
                await notifications.SendAsync(user, message);
            }
        });
    }
}
