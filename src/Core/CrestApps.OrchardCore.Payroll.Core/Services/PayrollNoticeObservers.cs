using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Services;
using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// Sends a notice to the user of a staff member, when Notifications and Users are on.
/// </summary>
internal static class StaffNotices
{
    public static async Task SendAsync(IServiceProvider services, string staffMemberId, NotificationMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(staffMemberId)
            || services.GetService<INotificationService>() is not { } notifications
            || services.GetService<UserManager<IUser>>() is not { } userManager)
        {
            return;
        }

        var staff = await services.GetRequiredService<IStaffDirectory>().GetAsync([staffMemberId], cancellationToken);

        if (staff.GetValueOrDefault(staffMemberId)?.UserId is { Length: > 0 } userId && await userManager.FindByIdAsync(userId) is { } user)
        {
            await notifications.SendAsync(user, message, cancellationToken);
        }
    }
}

/// <summary>
/// Tells a person that the bank returned a deposit of their pay, so they check their account details; their payroll
/// contact pays it another way. The notice names no account and no amount.
/// </summary>
public sealed class DepositReturnedNotificationObserver : IDisbursementObserver
{
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DepositReturnedNotificationObserver"/> class.
    /// </summary>
    /// <param name="services">The services, for notifications and users when those features are on.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DepositReturnedNotificationObserver(IServiceProvider services, IStringLocalizer<DepositReturnedNotificationObserver> stringLocalizer)
    {
        _services = services;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public Task OnChangedAsync(DisbursementChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        var disbursement = change.Disbursement;

        if (change.Kind != DisbursementChangeKind.Returned || disbursement?.SourceKind != PayStatementSources.Kind || disbursement.Payee.Kind != StaffConstants.PartyKind)
        {
            return Task.CompletedTask;
        }

        return StaffNotices.SendAsync(_services, disbursement.Payee.Id, new NotificationMessage
        {
            Subject = S["A deposit of your pay was returned"],
            Summary = S["The bank returned a deposit of your pay ({0}). Check your bank account details; your payroll contact will pay it another way.", disbursement.FailureReason ?? disbursement.FailureCode ?? string.Empty],
            Url = string.IsNullOrEmpty(disbursement.SourceId) ? MyPayUrls.Home : MyPayUrls.Statement(disbursement.SourceId),
            Type = PayrollConstants.DepositReturnedNotificationType,
            ReferenceId = disbursement.ItemId,
        }, cancellationToken);
    }
}

/// <summary>
/// A security notice: tells a staff member that a bank account of theirs was added or its number changed, whoever did
/// it, so a change they did not make does not go unseen. The notice shows only the account's name.
/// </summary>
public sealed class BankAccountChangedNotificationObserver : IBankAccountObserver
{
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BankAccountChangedNotificationObserver"/> class.
    /// </summary>
    /// <param name="services">The services, for notifications and users when those features are on.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BankAccountChangedNotificationObserver(IServiceProvider services, IStringLocalizer<BankAccountChangedNotificationObserver> stringLocalizer)
    {
        _services = services;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public Task OnChangedAsync(BankAccountChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        var account = change.Account;

        if (account is null || account.Owner.Kind != StaffConstants.PartyKind || !(change.Has(BankAccountChangeKind.Created) || change.Has(BankAccountChangeKind.NumberChanged)))
        {
            return Task.CompletedTask;
        }

        var actor = change.Actor ?? S["the system"].Value;

        return StaffNotices.SendAsync(_services, account.Owner.Id, new NotificationMessage
        {
            Subject = change.Has(BankAccountChangeKind.Created) ? S["A bank account was added for you"] : S["The number of your bank account {0} was changed", account.Name],
            Summary = S["{0} added or changed your bank account {1}. If you did not ask for this, tell your payroll contact now.", actor, account.Name],
            Url = MyPayUrls.Home,
            Type = PayrollConstants.BankAccountChangedNotificationType,
            ReferenceId = account.ItemId,
        }, cancellationToken);
    }
}
