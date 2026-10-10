using System.Text.Encodings.Web;
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
/// Delivers payment reminders. An authenticated owner is reached through the notification system so the
/// reminder honors the owner's channel preference; a guest owner (who has no user account) is reached by email
/// using the contact captured at purchase time, when the email feature is available.
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
        var title = GetTitle(transaction);
        var invoice = await PrepareInvoiceAsync(transaction, cancellationToken);

        // The first notice on the due date is the invoice itself coming due; later ones chase an unpaid balance.
        var dueToday = transaction.ReminderCount == 0 &&
            transaction.DueUtc.HasValue &&
            transaction.DueUtc.Value.Date == _clock.UtcNow.Date;

        string body;

        if (dueToday)
        {
            body = S["A payment of {0} for {1} is due today.", amount, title].Value;
        }
        else if (transaction.DueUtc.HasValue && transaction.DueUtc.Value.Date > _clock.UtcNow.Date)
        {
            // Sent by hand before the due date.
            body = S["This is a reminder that a payment of {0} for {1} is due on {2:d}.", amount, title, transaction.DueUtc.Value].Value;
        }
        else if (transaction.DueUtc.HasValue)
        {
            body = S["This is a reminder that you have an outstanding balance of {0} for {1}, which was due on {2:d}.", amount, title, transaction.DueUtc.Value].Value;
        }
        else
        {
            body = S["This is a reminder that you have an outstanding balance of {0} for {1}.", amount, title].Value;
        }

        var reminder = new Reminder
        {
            Subject = dueToday
                ? S["Payment due today: {0}", amount].Value
                : S["Payment reminder: {0} outstanding", amount].Value,
            Summary = dueToday
                ? S["A payment of {0} for {1} is due today.", amount, title].Value
                : S["You have an outstanding balance of {0} for {1}.", amount, title].Value,
            Body = body + invoice.Instructions,
            ActionUrl = invoice.PayUrl,
            ActionText = S["Pay {0}", amount].Value,
        };

        if (!await DeliverAsync(transaction, reminder, cancellationToken))
        {
            await KeepIssuedInvoiceNumberAsync(transaction, invoice, cancellationToken);

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

    /// <inheritdoc/>
    public async Task<bool> SendUpcomingReminderAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (string.IsNullOrEmpty(transaction.OwnerId) || transaction.OutstandingAmount <= 0m || !transaction.DueUtc.HasValue)
        {
            return false;
        }

        var amount = FormatAmount(transaction.OutstandingAmount, transaction.Currency);
        var title = GetTitle(transaction);
        var dueUtc = transaction.DueUtc.Value;
        var paymentMethod = transaction.AutoCollection?.PaymentMethodDescription;

        // A payment that will be taken automatically needs no action, only notice: the owner should know which
        // card is charged and when, so a charge is never a surprise and a card that will not work can be
        // replaced in time. A payment the owner makes themselves needs a call to action instead.
        var reminder = transaction.AutoCollection is not null
            ? new Reminder
            {
                Subject = S["Upcoming payment: {0} on {1:d}", amount, dueUtc].Value,
                Summary = string.IsNullOrEmpty(paymentMethod)
                    ? S["{0} for {1} will be charged to your card on file on {2:d}.", amount, title, dueUtc].Value
                    : S["{0} for {1} will be charged to {2} on {3:d}.", amount, title, paymentMethod, dueUtc].Value,
                Body = string.IsNullOrEmpty(paymentMethod)
                    ? S["This is a reminder that your scheduled payment of {0} for {1} will be charged to your card on file on {2:d}. You do not need to do anything. If the card has changed, please contact us before then.", amount, title, dueUtc].Value
                    : S["This is a reminder that your scheduled payment of {0} for {1} will be charged to {2} on {3:d}. You do not need to do anything. If the card has changed, please contact us before then.", amount, title, paymentMethod, dueUtc].Value,
            }
            : null;

        // A payment the owner makes themselves: the reminder is their invoice, with a link that pays it.
        var invoice = default(InvoiceDetails);

        if (reminder is null)
        {
            invoice = await PrepareInvoiceAsync(transaction, cancellationToken);
            reminder = new Reminder
            {
                Subject = S["Payment due on {1:d}: {0}", amount, dueUtc].Value,
                Summary = S["A payment of {0} for {1} is due on {2:d}.", amount, title, dueUtc].Value,
                Body = S["This is a reminder that a payment of {0} for {1} is due on {2:d}.", amount, title, dueUtc].Value + invoice.Instructions,
                ActionUrl = invoice.PayUrl,
                ActionText = S["Pay {0}", amount].Value,
            };
        }

        if (!await DeliverAsync(transaction, reminder, cancellationToken))
        {
            await KeepIssuedInvoiceNumberAsync(transaction, invoice, cancellationToken);

            return false;
        }

        var now = _clock.UtcNow;

        transaction.UpcomingReminderSentUtc = now;
        transaction.UpdatedUtc = now;
        transaction.Events.Add(new TransactionEvent
        {
            CreatedUtc = now,
            Type = TransactionEventType.ReminderSent,
            Message = S["A reminder that {0} is due on {1:d} was sent.", amount, dueUtc].Value,
        });

        return true;
    }

    // Numbers the invoice the first time the owner is told about it, and creates the link that pays it without signing
    // in. Both services are optional, so a site without them still sends a plain reminder.
    private async Task<InvoiceDetails> PrepareInvoiceAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        var issued = _serviceProvider.GetService<ITransactionInvoiceService>() is { } invoices &&
            await invoices.EnsureInvoiceNumberAsync(transaction, cancellationToken);

        var payUrl = _serviceProvider.GetService<ITransactionPayLinkService>() is { } payLinks
            ? await payLinks.CreatePayUrlAsync(transaction, cancellationToken)
            : null;

        var instructions = string.Empty;

        if (!string.IsNullOrEmpty(transaction.InvoiceNumber))
        {
            instructions += " " + S["Invoice {0}.", transaction.InvoiceNumber].Value;
        }

        if (!string.IsNullOrEmpty(payUrl))
        {
            instructions += " " + S["Pay it online, no sign-in needed: {0}", payUrl].Value;
        }
        else if (transaction.OwnerKind != CustomerOwnerKind.Guest)
        {
            instructions += " " + S["Please sign in to pay it."].Value;
        }

        return new InvoiceDetails(issued, payUrl, instructions);
    }

    // An invoice number is issued once. When the notice that carried it could not be delivered, the number is kept on
    // the transaction anyway, so the next attempt sends the same number instead of using up another.
    private async Task KeepIssuedInvoiceNumberAsync(Transaction transaction, InvoiceDetails invoice, CancellationToken cancellationToken)
    {
        if (invoice.Issued && _serviceProvider.GetService<ITransactionManager>() is { } manager)
        {
            await manager.UpdateAsync(transaction, data: null, cancellationToken);
        }
    }

    // The same text with the pay link as a button, for channels that show HTML.
    private static string BuildHtmlBody(Reminder reminder)
    {
        if (string.IsNullOrEmpty(reminder.ActionUrl))
        {
            return null;
        }

        var encoder = HtmlEncoder.Default;

        return $"<p>{encoder.Encode(reminder.Body)}</p>" +
            $"<p><a href=\"{encoder.Encode(reminder.ActionUrl)}\" style=\"display:inline-block;padding:10px 18px;background:#0d6efd;color:#ffffff;text-decoration:none;border-radius:6px;\">{encoder.Encode(reminder.ActionText)}</a></p>";
    }

    private string GetTitle(Transaction transaction)
        => string.IsNullOrEmpty(transaction.Title)
            ? S["your recent purchase"].Value
            : transaction.Title;

    private Task<bool> DeliverAsync(Transaction transaction, Reminder reminder, CancellationToken cancellationToken)
        => transaction.OwnerKind == CustomerOwnerKind.Guest
            ? SendGuestReminderAsync(transaction, reminder, cancellationToken)
            : SendAuthenticatedReminderAsync(transaction, reminder, cancellationToken);

    private async Task<bool> SendAuthenticatedReminderAsync(Transaction transaction, Reminder reminder, CancellationToken cancellationToken)
    {
        var user = await _userService.GetUserByUniqueIdAsync(transaction.OwnerId);

        if (user is null)
        {
            _logger.LogWarning("Unable to send a transaction reminder because the owner '{OwnerId}' was not found.", transaction.OwnerId);

            return false;
        }

        var htmlBody = BuildHtmlBody(reminder);
        var message = new NotificationMessage
        {
            Subject = reminder.Subject,
            Summary = reminder.Summary,
            TextBody = reminder.Body,
            HtmlBody = htmlBody,
            IsHtmlPreferred = htmlBody is not null,
        };

        var result = await _notificationService.SendAsync(user, message, cancellationToken);

        return result.SuccessfulCount > 0;
    }

    private async Task<bool> SendGuestReminderAsync(Transaction transaction, Reminder reminder, CancellationToken cancellationToken)
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

        var message = new MailMessage
        {
            To = contact.Email,
            Subject = reminder.Subject,
            TextBody = reminder.Body,
            HtmlBody = BuildHtmlBody(reminder),
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

    private sealed class Reminder
    {
        public string Subject { get; init; }

        public string Summary { get; init; }

        public string Body { get; init; }

        public string ActionUrl { get; init; }

        public string ActionText { get; init; }
    }

    private readonly record struct InvoiceDetails(bool Issued, string PayUrl, string Instructions);
}
