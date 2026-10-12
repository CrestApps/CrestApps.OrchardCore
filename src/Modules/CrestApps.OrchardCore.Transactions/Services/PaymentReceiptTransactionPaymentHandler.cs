using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Receipts;
using CrestApps.OrchardCore.Transactions.FinancialDocuments;
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
/// Sends the customer a receipt for every payment applied to a transaction, however it was paid, and a notice for
/// every refund of one, however the refund finished. A signed-up customer receives them through the notification
/// system, so they honor their channel preference; a guest receives them by email at the address captured when they
/// bought.
/// </summary>
public sealed class PaymentReceiptTransactionPaymentHandler : ITransactionPaymentHandler, IPaymentRefundHandler
{
    private readonly ITransactionReceiptBuilder _receiptBuilder;
    private readonly IReceiptHtmlRenderer _htmlRenderer;
    private readonly ITransactionManager _transactionManager;
    private readonly INotificationService _notificationService;
    private readonly IUserService _userService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentReceiptTransactionPaymentHandler"/> class.
    /// </summary>
    /// <param name="receiptBuilder">The builder for the receipt of one payment.</param>
    /// <param name="htmlRenderer">The renderer for the receipt's email body.</param>
    /// <param name="transactionManager">The transaction manager, used to record that the receipt was sent.</param>
    /// <param name="notificationService">The notification service used to reach a signed-up customer.</param>
    /// <param name="userService">The user service used to resolve a signed-up customer.</param>
    /// <param name="serviceProvider">The service provider the optional email service is resolved from.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PaymentReceiptTransactionPaymentHandler(
        ITransactionReceiptBuilder receiptBuilder,
        IReceiptHtmlRenderer htmlRenderer,
        ITransactionManager transactionManager,
        INotificationService notificationService,
        IUserService userService,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<PaymentReceiptTransactionPaymentHandler> logger,
        IStringLocalizer<PaymentReceiptTransactionPaymentHandler> stringLocalizer)
    {
        _receiptBuilder = receiptBuilder;
        _htmlRenderer = htmlRenderer;
        _transactionManager = transactionManager;
        _notificationService = notificationService;
        _userService = userService;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task PaymentRecordedAsync(TransactionPaymentRecordedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var transaction = context.Transaction;

        if (string.IsNullOrEmpty(transaction.OwnerId))
        {
            return;
        }

        // The receipt gets its short number before it is built and sent, and keeps it: every later copy of this
        // receipt shows the number the customer was sent.
        var numbered = await IssueReceiptNumberAsync(context.Payment, cancellationToken);

        var receipt = await _receiptBuilder.BuildAsync(transaction, context.Payment, cancellationToken);

        if (receipt is null)
        {
            if (numbered)
            {
                await _transactionManager.UpdateAsync(transaction, data: null, cancellationToken);
            }

            return;
        }

        var amount = $"{transaction.Currency} {CurrencyScale.Format(receipt.Total, transaction.Currency)}";
        var title = string.IsNullOrEmpty(transaction.Title) ? S["your purchase"].Value : transaction.Title;
        var subject = S["Receipt for your payment of {0}", amount].Value;
        var text = S["Thank you. We received your payment of {0} for {1}. Your receipt reference is {2}.", amount, title, receipt.Reference].Value;
        var html = _htmlRenderer.Render(receipt);

        var delivered = transaction.OwnerKind == CustomerOwnerKind.Guest
            ? await SendByEmailAsync(receipt.BilledToEmail, subject, text, html, cancellationToken)
            : await SendNotificationAsync(transaction.OwnerId, subject, text, html, cancellationToken);

        if (!delivered)
        {
            _logger.LogWarning("The receipt for a payment on transaction '{TransactionId}' could not be delivered to its owner.", transaction.ItemId);

            if (numbered)
            {
                await _transactionManager.UpdateAsync(transaction, data: null, cancellationToken);
            }

            return;
        }

        // The audit trail says the customer was sent proof of payment, which is what support is asked about.
        transaction.Events.Add(new TransactionEvent
        {
            CreatedUtc = _clock.UtcNow,
            Type = TransactionEventType.Note,
            Message = S["A receipt for {0} was sent to the customer.", amount].Value,
        });

        await _transactionManager.UpdateAsync(transaction, data: null, cancellationToken);
    }

    private async Task<bool> IssueReceiptNumberAsync(TransactionEvent payment, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(payment.ReceiptNumber) ||
            payment.Amount is not > 0m ||
            _serviceProvider.GetService<IFinancialDocumentNumberGenerator>() is not { } generator)
        {
            return false;
        }

        var number = await generator.GenerateAsync(new FinancialDocumentNumberRequest(FinancialDocumentKind.Receipt), cancellationToken);

        payment.ReceiptNumber = number.PublicToken;

        return true;
    }

    /// <inheritdoc/>
    public async Task RefundSucceededAsync(PaymentRefund refund, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refund);

        if (string.IsNullOrEmpty(refund.OriginalAttemptId) || _serviceProvider.GetService<IPaymentAttemptStore>() is not { } attemptStore)
        {
            return;
        }

        // The refund names the payment it returns; the payment names the transaction it paid for, and that is where
        // the customer is known.
        var attempt = await attemptStore.FindByIdAsync(refund.OriginalAttemptId, cancellationToken);

        if (attempt is null ||
            !string.Equals(attempt.ReferenceType, TransactionsConstants.ReferenceTypes.Transaction, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(attempt.ReferenceId))
        {
            return;
        }

        var transaction = await _transactionManager.FindByIdAsync(attempt.ReferenceId, cancellationToken);

        if (transaction is null || string.IsNullOrEmpty(transaction.OwnerId))
        {
            return;
        }

        var amount = $"{refund.Currency} {CurrencyScale.Format(refund.RefundGrossAmount, refund.Currency)}";
        var title = string.IsNullOrEmpty(transaction.Title) ? S["your purchase"].Value : transaction.Title;
        var subject = S["Your refund of {0}", amount].Value;
        var text = S["We refunded {0} for {1} to the card or account you paid with. It can take a few business days to appear on your statement.", amount, title].Value;

        var delivered = transaction.OwnerKind == CustomerOwnerKind.Guest
            ? await SendByEmailAsync(transaction.GuestContactEmail, subject, text, html: null, cancellationToken)
            : await SendNotificationAsync(transaction.OwnerId, subject, text, html: null, cancellationToken);

        if (!delivered)
        {
            _logger.LogWarning("The notice for refund '{RefundId}' on transaction '{TransactionId}' could not be delivered to its owner.", refund.ItemId, transaction.ItemId);

            return;
        }

        transaction.Events.Add(new TransactionEvent
        {
            CreatedUtc = _clock.UtcNow,
            Type = TransactionEventType.Note,
            Message = S["A refund of {0} was made, and the customer was told.", amount].Value,
        });

        await _transactionManager.UpdateAsync(transaction, data: null, cancellationToken);
    }

    private async Task<bool> SendNotificationAsync(string userId, string subject, string text, string html, CancellationToken cancellationToken)
    {
        var user = await _userService.GetUserByUniqueIdAsync(userId);

        if (user is null)
        {
            return false;
        }

        var result = await _notificationService.SendAsync(
            user,
            new NotificationMessage
            {
                Subject = subject,
                Summary = text,
                TextBody = text,
                HtmlBody = html,
                IsHtmlPreferred = html is not null,
            },
            cancellationToken);

        return result.SuccessfulCount > 0;
    }

    private async Task<bool> SendByEmailAsync(string email, string subject, string text, string html, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(email) || _serviceProvider.GetService<IEmailService>() is not { } emailService)
        {
            return false;
        }

        var result = await emailService.SendAsync(
            new MailMessage
            {
                To = email,
                Subject = subject,
                TextBody = text,
                HtmlBody = html,
            },
            cancellationToken: cancellationToken);

        return result.Succeeded;
    }
}
