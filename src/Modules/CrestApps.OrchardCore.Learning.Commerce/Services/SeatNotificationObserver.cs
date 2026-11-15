using CrestApps.OrchardCore.Learning.Commerce.Models;
using CrestApps.OrchardCore.Learning.Commerce.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Notifications;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.Commerce.Services;

/// <summary>
/// Tells a person their account gave them a seat in a course.
/// </summary>
internal sealed class SeatNotificationObserver : ILicensePoolObserver
{
    private readonly ShellSettings _shellSettings;

    internal readonly IStringLocalizer S;

    public SeatNotificationObserver(ShellSettings shellSettings, IStringLocalizer<SeatNotificationObserver> stringLocalizer)
    {
        _shellSettings = shellSettings;
        S = stringLocalizer;
    }

    public Task SeatGivenAsync(LicensePool pool, LicenseSeat seat, CancellationToken cancellationToken = default)
    {
        var prefix = string.IsNullOrEmpty(_shellSettings.RequestUrlPrefix) ? string.Empty : "/" + _shellSettings.RequestUrlPrefix;
        var message = new LearningNotificationMessage
        {
            Subject = S["You have a seat in {0}", pool.Title ?? string.Empty],
            Summary = S["Your account gave you a seat in {0}. It is in My courses.", pool.Title ?? string.Empty],
            TextBody = S["Your account gave you a seat in {0}. It is in My courses.", pool.Title ?? string.Empty],
            Type = LearningCommerceConstants.SeatGivenNotificationType,
            ReferenceId = seat.ItemId,
            Url = prefix + "/learning",
        };
        var userId = seat.UserId;

        ShellScope.AddDeferredTask(async scope =>
        {
            var services = scope.ServiceProvider;
            var user = await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync(userId);

            if (user is not null)
            {
                await services.GetRequiredService<INotificationService>().SendAsync(user, message);
            }
        });

        return Task.CompletedTask;
    }
}
