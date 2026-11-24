using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Api;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Invoicing.AI;

/// <summary>
/// Records money received outside the site against an open invoice, as the invoice's own payment endpoint does
/// (<c>POST invoices/{id}/payments</c>: <see cref="ITransactionCommandService.RecordPaymentAsync"/> on the invoice's
/// transaction, which locks, checks and commits it, then <see cref="InvoiceLedgerSync"/>). It follows the owner's money
/// rules (F23): the amount and the invoice's currency are always given, the amount is capped by what the ledger still has
/// owed (so nothing is left unapplied), paying everything needs <c>entireBalance</c>, every call carries the token
/// <c>previewInvoiceAction</c> issued for the same payment and a new idempotency key, and the reference and note never
/// hold a card or bank account number. When the money was received is entered in the site's time zone, as the
/// transaction's Record payment form takes it (<see cref="CrestApps.OrchardCore.AI.Core.AILocalTimeArguments"/>), and is part of the token's
/// fingerprint.
/// </summary>
/// <remarks>
/// The token is taken inside the idempotent operation, so a retry with the same key gets the first answer back without a
/// new token, and a refused token or a payment the ledger refused keeps the key free. A payment that may have been saved
/// keeps the key, so it is never recorded twice.
/// </remarks>
internal sealed class RecordInvoicePaymentTool : InvoiceActionToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "recordInvoicePayment";

    private const string Refused = "Nothing was recorded";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "invoice": { "type": "string", "description": "The invoice's number, such as INV-1001, or its id." },
            "amount": { "type": "number", "description": "The amount received, more than zero and no more than what the ledger still has owed, in the invoice's currency." },
            "currency": { "type": "string", "description": "The invoice's currency, such as USD." },
            "entireBalance": { "type": "boolean", "description": "Set to true when the payment covers everything still owed; amount must then be exactly that. An amount equal to what is owed needs it." },
            "method": { "type": "string", "enum": ["cash", "check", "bank-transfer", "card-terminal", "other"], "description": "How the money was received." },
            "reference": { "type": "string", "description": "A check number or a short transfer reference, up to 100 characters. Never a card or bank account number." },
            "note": { "type": "string", "description": "A note kept with the payment, up to 500 characters." },
            "receivedAt": { "type": "string", "description": "When the money was received, yyyy-MM-ddTHH:mm in the site's time zone, such as a check dated last week, as previewed. Not in the future. Left out, it was received now." },
            "previewToken": { "type": "string", "description": "The previewToken previewInvoiceAction returned for this invoice with the recordPayment action and the same amount, currency, entireBalance, method, reference, note and receivedAt. It works once and expires 10 minutes after the preview." },
            "idempotencyKey": { "type": "string", "description": "A new unique key for this payment, up to 128 characters, such as payment-inv1001-check4471. A second call with the same key is refused, so a retry never records the payment twice." }
          },
          "required": ["invoice", "amount", "currency", "method", "previewToken", "idempotencyKey"],
          "additionalProperties": false
        }
        """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Records money received outside the site (cash, check, bank transfer, card terminal) against an open invoice: an amount in its currency up to what the ledger still has owed, or all of it with entireBalance, and when it was received. No money moves through the site; the entry can't be removed from a chat and the customer may be e-mailed a receipt. Call previewInvoiceAction with the recordPayment action first and show the result to the user: this tool needs the previewToken it returns and a new idempotencyKey. Never give a card or bank account number.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, caller, InvoicingPermissions.RecordInvoicePayments))
        {
            return NotAllowedMessage;
        }

        var key = ReadKey(arguments);

        if (key is null)
        {
            return $"{Refused}: give a new idempotencyKey of up to {MaxKeyLength} characters for this payment.";
        }

        var (invoice, error) = await FindInvoiceAsync(services, Read(arguments, "invoice"), cancellationToken);

        if (invoice is null)
        {
            return error;
        }

        var (request, refusal) = await ReadActionAsync(services, arguments, invoice, RecordPaymentAction, Refused, cancellationToken);

        if (request is null)
        {
            return refusal;
        }

        var previewToken = Read(arguments, "previewToken");

        return await RunOnceAsync(services, caller, key, request.RequestFingerprint, "recorded", async token =>
        {
            if (await WriteEarlierWorkAsync(services, token) is { } earlier)
            {
                return new IdempotentOperationAnswer(earlier, Keep: false);
            }

            if (await TakePreviewTokenAsync(services, caller, previewToken, request.Fingerprint, Refused, PreviewToolName, token) is { } tokenError)
            {
                return new IdempotentOperationAnswer(tokenError, Keep: false);
            }

            TransactionCommandResult result;

            try
            {
                result = await services.GetRequiredService<ITransactionCommandService>().RecordPaymentAsync(request.Invoice.TransactionId, new TransactionPaymentRequest
                {
                    Amount = request.Amount,
                    PaymentMethod = request.Method,
                    ReceivedUtc = request.ReceivedUtc,
                    Reference = request.Reference,
                    Note = request.Note,
                }, token);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The payment may already be saved (the payment handlers run after the save), so the key stays used.
                MarkSessionClosed(services);

                return new IdempotentOperationAnswer($"Recording the payment on invoice {request.Invoice.Number} stopped with an error, and it may already be recorded. Do not retry with this or another key; read the invoice with getInvoice.", Keep: true);
            }

            if (!result.Succeeded)
            {
                // Nothing was recorded, so the key may be used again with a new preview.
                return new IdempotentOperationAnswer($"{Refused}: {string.Join(" ", result.Errors)}", Keep: false);
            }

            // The ledger committed the payment; the invoice follows it (its payment handler queued the update too).
            var current = request.Invoice;

            if (services.GetService<InvoiceLedgerSync>() is { } sync)
            {
                current = await sync.SyncAsync(current, token) ?? current;
            }

            var saved = await SaveAsync(services, token);
            var recorded = result.Event?.Amount ?? request.Amount;
            var receivedAt = await LocalTimeAsync(services, result.Event?.ReceivedUtc ?? request.ReceivedUtc);

            var answer = Json(new
            {
                id = current.ItemId,
                number = current.Number,
                recorded,
                currency = current.Currency,
                method = request.Method,
                reference = request.Reference,
                receivedAt,
                amountDue = current.AmountDue,
                settlement = ToCamel(current.Settlement.ToString()),
                unapplied = result.UnappliedAmount > 0m ? result.UnappliedAmount : (decimal?)null,
                invoiceUpdated = saved,
                adminUrl = InvoiceAdminPath(services, current.ItemId),
                message = saved
                    ? $"A payment of {Format(recorded, current.Currency)} was recorded on invoice {current.Number}. No money moved through the site."
                    : $"A payment of {Format(recorded, current.Currency)} was recorded on the ledger for invoice {current.Number}; the invoice catches up when its page is opened. Do not record it again.",
            });

            return new IdempotentOperationAnswer(answer, Keep: true);
        }, cancellationToken);
    }
}
