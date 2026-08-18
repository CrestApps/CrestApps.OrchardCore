using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Email;
using OrchardCore.Modules;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Delivers outstanding-payment reminders. An authenticated owner is reached through the notification
/// system so the reminder honors the owner's channel preference; a guest owner (who has no user account) is
/// reached by email using the contact captured at purchase time, when the email feature is available.
/// </summary>
public sealed class DefaultTransactionReminderService : ITransactionReminderService
{
    private readonly INotificationService _notificationService;
    private readonly IUserService _userService;
    private readonly ICustomerContactResolver _contactResolver;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTransactionReminderService"/> class.
    /// </summary>
    /// <param name="notificationService">The notification service used to reach an authenticated owner.</param>
    /// <param name="userService">The user service used to resolve an authenticated owner.</param>
    /// <param name="contactResolver">The resolver that addresses the owner uniformly.</param>
    /// <param name="serviceProvider">The service provider used to resolve the optional email service for guest delivery.</param>
    /// <param name="clock">The clock used to stamp the reminder.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DefaultTransactionReminderService(
        INotificationService notificationService,
        IUserService userService,
        ICustomerContactResolver contactResolver,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<DefaultTransactionReminderService> logger,
        IStringLocalizer<DefaultTransactionReminderService> stringLocalizer)
    {
        _notificationService = notificationService;
        _userService = userService;
        _contactResolver = contactResolver;
        _serviceProvider = serviceProvider;
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

        var amount = FormatAmount(transaction.OutstandingAmount, transaction.Currency);
        var title = string.IsNullOrEmpty(transaction.Title)
            ? S["your recent purchase"].Value
            : transaction.Title;

        var delivered = transaction.OwnerKind == CustomerOwnerKind.Guest
            ? await SendGuestReminderAsync(transaction, amount, title, cancellationToken)
            : await SendAuthenticatedReminderAsync(transaction, amount, title, cancellationToken);

        if (!delivered)
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

    private async Task<bool> SendAuthenticatedReminderAsync(Transaction transaction, string amount, string title, CancellationToken cancellationToken)
    {
        var user = await _userService.GetUserByUniqueIdAsync(transaction.OwnerId);

        if (user is null)
        {
            _logger.LogWarning("Unable to send a transaction reminder because the owner '{OwnerId}' was not found.", transaction.OwnerId);

            return false;
        }

        var message = new NotificationMessage
        {
            Subject = S["Payment reminder: {0} outstanding", amount],
            Summary = S["You have an outstanding balance of {0} for {1}.", amount, title],
            TextBody = transaction.DueUtc.HasValue
                ? S["This is a reminder that you have an outstanding balance of {0} for {1}, due on {2:d}. Please sign in to settle it.", amount, title, transaction.DueUtc.Value].Value
                : S["This is a reminder that you have an outstanding balance of {0} for {1}. Please sign in to settle it.", amount, title].Value,
        };

        var result = await _notificationService.SendAsync(user, message, cancellationToken);

        return result.SuccessfulCount > 0;
    }

    private async Task<bool> SendGuestReminderAsync(Transaction transaction, string amount, string title, CancellationToken cancellationToken)
    {
        var owner = CustomerOwner.ForGuest(transaction.OwnerId);
        var guestContact = new CustomerContact
        {
            DisplayName = transaction.GuestContactName,
            Email = transaction.GuestContactEmail,
        };

        var contact = await _contactResolver.ResolveAsync(owner, guestContact, cancellationToken);

        if (contact is null || string.IsNullOrEmpty(contact.Email))
        {
            _logger.LogWarning("Unable to send a transaction reminder because the guest owner '{OwnerId}' has no contact email.", transaction.OwnerId);

            return false;
        }

        var emailService = _serviceProvider.GetService<IEmailService>();

        if (emailService is null)
        {
            _logger.LogWarning("Unable to send a guest transaction reminder because no email service is registered.");

            return false;
        }

        var body = transaction.DueUtc.HasValue
            ? S["This is a reminder that you have an outstanding balance of {0} for {1}, due on {2:d}. Please settle it at your earliest convenience.", amount, title, transaction.DueUtc.Value].Value
            : S["This is a reminder that you have an outstanding balance of {0} for {1}. Please settle it at your earliest convenience.", amount, title].Value;

        var message = new MailMessage
        {
            To = contact.Email,
            Subject = S["Payment reminder: {0} outstanding", amount].Value,
            TextBody = body,
        };

        var result = await emailService.SendAsync(message, cancellationToken: cancellationToken);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Unable to deliver a guest transaction reminder email to the owner '{OwnerId}'.", transaction.OwnerId);

            return false;
        }

        return true;
    }

    private static string FormatAmount(decimal amount, string currency)
    {
        var formatted = CurrencyScale.Format(amount, currency);

        return string.IsNullOrEmpty(currency)
            ? formatted
            : $"{currency} {formatted}";
    }
}
