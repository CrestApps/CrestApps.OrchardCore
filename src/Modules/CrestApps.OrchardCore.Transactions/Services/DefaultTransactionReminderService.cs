using System.Globalization;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Delivers outstanding-payment reminders through the notification system so each reminder honors the
/// owner's channel preference rather than assuming email only.
/// </summary>
public sealed class DefaultTransactionReminderService : ITransactionReminderService
{
    private readonly INotificationService _notificationService;
    private readonly IUserService _userService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTransactionReminderService"/> class.
    /// </summary>
    /// <param name="notificationService">The notification service used to deliver the reminder.</param>
    /// <param name="userService">The user service used to resolve the transaction owner.</param>
    /// <param name="clock">The clock used to stamp the reminder.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DefaultTransactionReminderService(
        INotificationService notificationService,
        IUserService userService,
        IClock clock,
        ILogger<DefaultTransactionReminderService> logger,
        IStringLocalizer<DefaultTransactionReminderService> stringLocalizer)
    {
        _notificationService = notificationService;
        _userService = userService;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<bool> SendReminderAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (string.IsNullOrEmpty(transaction.OwnerId) || transaction.OutstandingAmount <= 0m)
        {
            return false;
        }

        var user = await _userService.GetUserByUniqueIdAsync(transaction.OwnerId);

        if (user == null)
        {
            _logger.LogWarning("Unable to send a transaction reminder because the owner '{OwnerId}' was not found.", transaction.OwnerId);

            return false;
        }

        var amount = FormatAmount(transaction.OutstandingAmount, transaction.Currency);
        var title = string.IsNullOrEmpty(transaction.Title)
            ? S["your recent purchase"].Value
            : transaction.Title;

        var message = new NotificationMessage
        {
            Subject = S["Payment reminder: {0} outstanding", amount],
            Summary = S["You have an outstanding balance of {0} for {1}.", amount, title],
            TextBody = transaction.DueUtc.HasValue
                ? S["This is a reminder that you have an outstanding balance of {0} for {1}, due on {2:d}. Please sign in to settle it.", amount, title, transaction.DueUtc.Value].Value
                : S["This is a reminder that you have an outstanding balance of {0} for {1}. Please sign in to settle it.", amount, title].Value,
        };

        var result = await _notificationService.SendAsync(user, message, cancellationToken);

        if (result.SuccessfulCount == 0)
        {
            return false;
        }

        var now = _clock.UtcNow;

        transaction.ReminderCount++;
        transaction.LastReminderSentUtc = now;
        transaction.UpdatedUtc = now;
        transaction.Events.Add(new TransactionEvent
        {
            CreatedUtc = now,
            Type = TransactionEventType.ReminderSent,
            Message = S["A payment reminder for {0} was sent.", amount].Value,
        });

        return true;
    }

    private static string FormatAmount(decimal amount, string currency)
    {
        var formatted = amount.ToString("0.00", CultureInfo.InvariantCulture);

        return string.IsNullOrEmpty(currency)
            ? formatted
            : $"{currency} {formatted}";
    }
}
