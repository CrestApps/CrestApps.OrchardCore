using System.Globalization;
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
/// Tells a person their pay statement is ready, through the notification center and the methods they chose (e-mail,
/// SMS), with a link to the statement in My Pay. A notice carries no amount and no attachment; a voided statement's
/// notice is removed.
/// </summary>
public sealed class PayStatementNotificationObserver : IPayStatementObserver
{
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayStatementNotificationObserver"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services, for notifications and users when those features are on.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayStatementNotificationObserver(IServiceProvider serviceProvider, IStringLocalizer<PayStatementNotificationObserver> stringLocalizer)
    {
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the address of a statement in My Pay.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns>The address.</returns>
    public static string StatementUrl(PayStatement statement)
        => "~/my-pay/statements/" + statement.ItemId;

    /// <inheritdoc />
    public async Task IssuedAsync(PayStatement statement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statement);

        var notifications = _serviceProvider.GetService<INotificationService>();
        var userManager = _serviceProvider.GetService<UserManager<IUser>>();

        if (notifications is null || userManager is null)
        {
            return;
        }

        var staff = await _serviceProvider.GetRequiredService<IStaffDirectory>().GetAsync([statement.StaffMemberId], cancellationToken);

        if (staff.GetValueOrDefault(statement.StaffMemberId)?.UserId is not { Length: > 0 } userId || await userManager.FindByIdAsync(userId) is not { } user)
        {
            return;
        }

        var payDate = statement.PayDate.ToString("d", CultureInfo.CurrentCulture);

        await notifications.SendAsync(user, new NotificationMessage
        {
            Subject = S["Your pay statement for {0} is ready", payDate],
            Summary = S["{0} issued your pay statement for the pay of {1}.", statement.Snapshot?.EmployerName ?? string.Empty, payDate],
            TextBody = S["Sign in and open My Pay to see it."],
            Url = StatementUrl(statement),
            Type = PayrollConstants.PayStatementReadyNotificationType,
            ReferenceId = statement.ItemId,
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task VoidedAsync(PayStatement statement, PayStatement reversal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statement);

        if (_serviceProvider.GetService<INotificationManager>() is { } manager)
        {
            await manager.DeleteAsync(new NotificationFilter
            {
                Type = PayrollConstants.PayStatementReadyNotificationType,
                ReferenceId = statement.ItemId,
            }, cancellationToken);
        }
    }
}
