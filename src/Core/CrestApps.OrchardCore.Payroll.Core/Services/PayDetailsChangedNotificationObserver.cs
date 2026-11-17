using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// A security notice: tells a person that their tax elections or where their pay goes changed, whoever changed them, so a
/// change they did not make does not go unseen. It says what changed and who changed it, never an account number.
/// </summary>
public sealed class PayDetailsChangedNotificationObserver : IPayProfileObserver
{
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayDetailsChangedNotificationObserver"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services, for notifications and users when those features are on.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayDetailsChangedNotificationObserver(IServiceProvider serviceProvider, IStringLocalizer<PayDetailsChangedNotificationObserver> stringLocalizer)
    {
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task ChangedAsync(PayProfileChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (change.Kind is not (PayProfileChangeKind.DistributionChanged or PayProfileChangeKind.ElectionAdded) || change.Profile is null)
        {
            return;
        }

        var notifications = _serviceProvider.GetService<INotificationService>();
        var userManager = _serviceProvider.GetService<UserManager<IUser>>();

        if (notifications is null || userManager is null)
        {
            return;
        }

        var staff = await _serviceProvider.GetRequiredService<IStaffDirectory>().GetAsync([change.Profile.StaffMemberId], cancellationToken);

        if (staff.GetValueOrDefault(change.Profile.StaffMemberId)?.UserId is not { Length: > 0 } userId || await userManager.FindByIdAsync(userId) is not { } user)
        {
            return;
        }

        var actor = change.Actor ?? S["the system"].Value;

        await notifications.SendAsync(user, new NotificationMessage
        {
            Subject = change.Kind == PayProfileChangeKind.DistributionChanged ? S["Where your pay goes was changed"] : S["Your tax election was changed"],
            Summary = change.Kind == PayProfileChangeKind.DistributionChanged
                ? S["{0} changed the bank accounts or checks your pay is paid by. If you did not ask for this, tell your payroll contact now.", actor]
                : S["{0} added a tax election to your pay. If you did not ask for this, tell your payroll contact now.", actor],
            Url = MyPayUrls.Home,
            Type = PayrollConstants.PayDetailsChangedNotificationType,
            ReferenceId = change.Profile.ItemId,
        }, cancellationToken);
    }
}

/// <summary>
/// The addresses of My Pay, for links from notices.
/// </summary>
public static class MyPayUrls
{
    /// <summary>The home of My Pay.</summary>
    public const string Home = "~/Admin/my-pay";

    /// <summary>
    /// Gets the address of a statement in My Pay.
    /// </summary>
    /// <param name="statementId">The statement's id.</param>
    /// <returns>The address.</returns>
    public static string Statement(string statementId)
        => "~/Admin/my-pay/statements/" + statementId;

    /// <summary>
    /// Gets the address where a person reviews their coming pay.
    /// </summary>
    /// <param name="statementId">The draft statement's id.</param>
    /// <returns>The address.</returns>
    public static string Review(string statementId)
        => "~/Admin/my-pay/review/" + statementId;
}
