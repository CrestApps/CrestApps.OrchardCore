using System.Globalization;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Transactions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Invoicing.AI;

/// <summary>
/// What <c>previewInvoiceAction</c> and the tools that void, write off and record payments on an invoice share: the
/// checks of each action, made the same way by the preview and by the action, so a preview's token is issued only for an
/// action the tool would accept, and the action's fingerprint, which the token is bound to. The fingerprint holds what the
/// ledger still had owed and the invoice's state at the preview, so a payment or credit that lands in between makes the
/// token fail and the user sees a new preview.
/// </summary>
internal abstract class InvoiceActionToolBase : InvoiceMoneyAIToolBase
{
    /// <summary>
    /// The preview tool the actions require a token from.
    /// </summary>
    protected const string PreviewToolName = "previewInvoiceAction";

    /// <summary>
    /// The void action.
    /// </summary>
    protected const string VoidAction = "void";

    /// <summary>
    /// The write-off action.
    /// </summary>
    protected const string WriteOffAction = "writeOff";

    /// <summary>
    /// The record-payment action.
    /// </summary>
    protected const string RecordPaymentAction = "recordPayment";

    /// <summary>
    /// The longest payment reference.
    /// </summary>
    protected const int MaxReferenceLength = 100;

    /// <summary>
    /// The longest payment note.
    /// </summary>
    protected const int MaxNoteLength = 500;

    /// <summary>
    /// Reads and checks an action's arguments against the invoice and its ledger.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="arguments">The arguments.</param>
    /// <param name="invoice">The invoice.</param>
    /// <param name="action">The action: <see cref="VoidAction"/>, <see cref="WriteOffAction"/> or <see cref="RecordPaymentAction"/>.</param>
    /// <param name="refused">How a refusal starts, such as "Nothing was voided".</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The checked action, or the sentence that says why it is refused.</returns>
    protected static async Task<(InvoiceActionRequest Request, string Error)> ReadActionAsync(
        IServiceProvider services,
        AIFunctionArguments arguments,
        Invoice invoice,
        string action,
        string refused,
        CancellationToken cancellationToken)
    {
        if (!TryReadDecimal(arguments, "amount", out var amount))
        {
            return (null, $"{refused}: amount must be a number.");
        }

        var currency = Read(arguments, "currency");
        var name = invoice.Number ?? invoice.ItemId;

        if (invoice.Status != InvoiceStatus.Open)
        {
            return (null, action == VoidAction
                ? $"{refused}: only an open invoice can be voided; invoice {name} is {ToCamel(invoice.Status.ToString())}."
                : action == WriteOffAction
                    ? $"{refused}: only an open invoice with something still owed can be written off; invoice {name} is {ToCamel(invoice.Status.ToString())}."
                    : $"{refused}: only an open invoice takes a payment; invoice {name} is {ToCamel(invoice.Status.ToString())}.");
        }

        var ledger = await ReadLedgerAsync(services, invoice, refused, cancellationToken);

        if (ledger.Error is not null)
        {
            return (null, ledger.Error);
        }

        invoice = ledger.Invoice;

        var hasSchedule = invoice.Schedule is { Count: > 0 } || !string.IsNullOrEmpty(invoice.InstallmentPlanId);

        switch (action)
        {
            case VoidAction:
                if (invoice.AmountPaid > 0m || !InvoiceLifecycle.CanVoid(invoice))
                {
                    return (null, $"{refused}: something was paid on invoice {name}, so it can't be voided. Correct it with a credit note instead.");
                }

                if (invoice.AmountCredited > 0m)
                {
                    return (null, $"{refused}: a credit note was issued for invoice {name}, so it can't be voided. Issue a credit note for the rest instead.");
                }

                if (CheckWholeAmount(amount, currency, invoice.Currency, ledger.Outstanding, refused, $"the void cancels everything still owed on invoice {name}") is { } voidError)
                {
                    return (null, voidError);
                }

                return (new InvoiceActionRequest(ledger, action, amount.Value, false, null, null, null, Fingerprint("invoice-" + action, invoice.ItemId, amount.Value, invoice.Currency, ledger.Outstanding, invoice.Status, invoice.AmountPaid, invoice.AmountCredited)), null);

            case WriteOffAction:
                if (!string.IsNullOrEmpty(invoice.InstallmentPlanId))
                {
                    return (null, $"{refused}: invoice {name} is split into a payment schedule; write off or cancel its payments on the ledger instead.");
                }

                if (!InvoiceLifecycle.CanWriteOff(invoice) || ledger.Outstanding <= 0m)
                {
                    return (null, $"{refused}: nothing is owed on invoice {name} any more, so there is nothing to write off.");
                }

                if (CheckWholeAmount(amount, currency, invoice.Currency, ledger.Outstanding, refused, $"a write-off takes everything still owed on invoice {name}") is { } writeOffError)
                {
                    return (null, writeOffError);
                }

                return (new InvoiceActionRequest(ledger, action, amount.Value, false, null, null, null, Fingerprint("invoice-" + action, invoice.ItemId, amount.Value, invoice.Currency, ledger.Outstanding, invoice.Status, invoice.AmountPaid, invoice.AmountCredited)), null);

            case RecordPaymentAction:
                if (string.IsNullOrEmpty(invoice.TransactionId) || ledger.Transaction is null)
                {
                    return (null, $"{refused}: invoice {name} posted nothing to the ledger, so there is nothing to pay.");
                }

                if (hasSchedule)
                {
                    return (null, $"{refused}: invoice {name} is split into a payment schedule. Record each payment on its own part from the invoice's page, or with recordTransactionPayment on that part's transaction.");
                }

                var entireBalance = ReadBool(arguments, "entireBalance") == true;

                if (CheckAmount(amount, currency, invoice.Currency, ledger.Outstanding, entireBalance, refused, $"still owed on invoice {name}") is { } amountError)
                {
                    return (null, amountError);
                }

                var method = Read(arguments, "method")?.ToLowerInvariant();

                if (method is null || !TransactionsConstants.OfflinePaymentMethods.All.Contains(method, StringComparer.Ordinal))
                {
                    return (null, $"{refused}: give how the money was received: {string.Join(", ", TransactionsConstants.OfflinePaymentMethods.All)}.");
                }

                var reference = Read(arguments, "reference");
                var note = Read(arguments, "note");

                if (reference?.Length > MaxReferenceLength || note?.Length > MaxNoteLength)
                {
                    return (null, $"{refused}: the reference is limited to {MaxReferenceLength} characters and the note to {MaxNoteLength}.");
                }

                if (RefuseAccountNumbers(refused, reference, note) is { } numberError)
                {
                    return (null, numberError);
                }

                // When the money arrived, entered in the site's time zone as the transaction's Record payment form takes
                // it, stored in UTC; the ledger refuses a time in the future too.
                var receivedAt = Read(arguments, "receivedAt");
                DateTime? receivedUtc = null;

                if (receivedAt is not null)
                {
                    (receivedUtc, var receivedError) = await AILocalTimeArguments.ReadPastAsync(
                        services.GetRequiredService<ILocalClock>(),
                        services.GetRequiredService<IClock>(),
                        receivedAt,
                        refused,
                        "receivedAt",
                        "the time the payment was received");

                    if (receivedError is not null)
                    {
                        return (null, receivedError);
                    }
                }

                var received = receivedUtc?.ToString("O", CultureInfo.InvariantCulture);

                return (new InvoiceActionRequest(ledger, action, amount.Value, entireBalance, method, reference, note, Fingerprint("invoice-" + action, invoice.ItemId, amount.Value, invoice.Currency, entireBalance, method, reference, note, received, ledger.Outstanding, invoice.Status, invoice.AmountPaid, invoice.AmountCredited))
                {
                    // The idempotency key is bound to the payment asked for, not to the state at the time, so a retry of a
                    // payment that was recorded gets its first answer back.
                    RequestFingerprint = Fingerprint("invoice-" + action, invoice.ItemId, amount.Value, invoice.Currency, entireBalance, method, reference, note, received),
                    ReceivedUtc = receivedUtc,
                }, null);

            default:
                return (null, $"{refused}: the action must be {VoidAction}, {WriteOffAction} or {RecordPaymentAction}.");
        }
    }

    /// <summary>
    /// Reads a reason for a void or a write-off: required, as on the page, at most <see cref="InvoiceAIToolBase.MaxReasonLength"/>
    /// characters, and never holding a card or bank account number.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="refused">How a refusal starts.</param>
    /// <param name="what">What the reason is for, such as "voided".</param>
    /// <returns>The reason, or the sentence that says why it is refused.</returns>
    protected static (string Reason, string Error) ReadReason(AIFunctionArguments arguments, string refused, string what)
    {
        var reason = Read(arguments, "reason");

        if (reason is null)
        {
            return (null, $"{refused}: say why the invoice is {what}.");
        }

        if (reason.Length > MaxReasonLength)
        {
            return (null, $"{refused}: the reason is longer than {MaxReasonLength} characters.");
        }

        return RefuseAccountNumbers(refused, reason) is { } error ? (null, error) : (reason, null);
    }

    // A void or a write-off takes everything the ledger still has owed; the amount is given to confirm that figure. A
    // zero-total invoice is voided with an amount of 0.
    private static string CheckWholeAmount(decimal? amount, string currency, string documentCurrency, decimal outstanding, string refused, string description)
    {
        if (amount is null || amount < 0m)
        {
            return $"{refused}: give the amount, {Format(outstanding, documentCurrency)}: {description}.";
        }

        if (!string.Equals(currency?.Trim(), documentCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return $"{refused}: the invoice is in {documentCurrency}; give the amount in {documentCurrency}.";
        }

        return amount.Value != outstanding
            ? $"{refused}: {description}, {Format(outstanding, documentCurrency)}, not {amount.Value.ToString(CultureInfo.InvariantCulture)}. Give that amount."
            : null;
    }
}

/// <summary>
/// An invoice action whose arguments passed the checks.
/// </summary>
/// <param name="Ledger">The invoice, its transaction and what it still has owed.</param>
/// <param name="Action">The action.</param>
/// <param name="Amount">The amount, in the invoice's currency.</param>
/// <param name="EntireBalance">Whether a payment covers everything still owed.</param>
/// <param name="Method">How a payment was received.</param>
/// <param name="Reference">A payment's reference.</param>
/// <param name="Note">A payment's note.</param>
/// <param name="Fingerprint">The action's fingerprint, which a preview's token is bound to.</param>
internal sealed record InvoiceActionRequest(
    InvoiceLedgerBalance Ledger,
    string Action,
    decimal Amount,
    bool EntireBalance,
    string Method,
    string Reference,
    string Note,
    string Fingerprint)
{
    private readonly string _requestFingerprint;

    /// <summary>
    /// Gets the invoice.
    /// </summary>
    public Invoice Invoice
        => Ledger.Invoice;

    /// <summary>
    /// Gets when a payment's money was received, in UTC, when it was not now.
    /// </summary>
    public DateTime? ReceivedUtc { get; init; }

    /// <summary>
    /// Gets the fingerprint of what was asked, without the invoice's state, which an idempotency key is bound to; the
    /// action's <see cref="Fingerprint"/> when it is not set.
    /// </summary>
    public string RequestFingerprint
    {
        get => _requestFingerprint ?? Fingerprint;
        init => _requestFingerprint = value;
    }
}
