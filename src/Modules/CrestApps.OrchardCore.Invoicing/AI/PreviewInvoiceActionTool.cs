using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Invoicing.AI;

/// <summary>
/// Shows what <c>voidInvoice</c>, <c>writeOffInvoice</c> or <c>recordInvoicePayment</c> would do, without changing
/// anything (the owner's review, F23): what the ledger still has owed on the invoice, the amount, and the invoice after.
/// It applies the action's checks (<see cref="InvoiceActionToolBase.ReadActionAsync"/>), so a preview is issued only for
/// an action the tool would accept, and returns the token the action requires: tied to the caller and to the action's
/// fingerprint, used once, and valid for 10 minutes.
/// </summary>
internal sealed class PreviewInvoiceActionTool : InvoiceActionToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = PreviewToolName;

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "invoice": { "type": "string", "description": "The invoice's number, such as INV-1001, or its id. To waive a late fee, give the fee's debit note and the void action." },
            "action": { "type": "string", "enum": ["void", "writeOff", "recordPayment"], "description": "void cancels an open invoice nothing was paid or credited on; writeOff writes off what is still owed; recordPayment records money received outside the site." },
            "amount": { "type": "number", "description": "For void and writeOff, everything the ledger still has owed on the invoice (getInvoice's amountDue; 0 to void a zero-total invoice). For recordPayment, the amount received, more than zero and no more than what is still owed." },
            "currency": { "type": "string", "description": "The invoice's currency, such as USD." },
            "entireBalance": { "type": "boolean", "description": "recordPayment only: set to true when the payment covers everything still owed; amount must then be exactly that." },
            "method": { "type": "string", "enum": ["cash", "check", "bank-transfer", "card-terminal", "other"], "description": "recordPayment only: how the money was received." },
            "reference": { "type": "string", "description": "recordPayment only: a check number or a short transfer reference, up to 100 characters. Never a card or bank account number." },
            "note": { "type": "string", "description": "recordPayment only: a note kept with the payment, up to 500 characters." },
            "receivedAt": { "type": "string", "description": "recordPayment only: when the money was received, yyyy-MM-ddTHH:mm in the site's time zone, such as a check dated last week. Not in the future. Left out, it was received now." }
          },
          "required": ["invoice", "action", "amount", "currency"],
          "additionalProperties": false
        }
        """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Shows what voidInvoice, writeOffInvoice or recordInvoicePayment would do, without changing anything: what the ledger still has owed on the invoice, the amount, and the invoice after. Call it before every void, write-off or payment and show the result to the user. It returns a previewToken the action requires: it works once, only for you and for the same invoice, action and arguments, and expires after 10 minutes.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var action = Read(arguments, "action");
        var permission = action switch
        {
            VoidAction or WriteOffAction => InvoicingPermissions.VoidInvoices,
            RecordPaymentAction => InvoicingPermissions.RecordInvoicePayments,
            _ => null,
        };

        if (permission is null)
        {
            return $"Nothing was previewed: the action must be {VoidAction}, {WriteOffAction} or {RecordPaymentAction}.";
        }

        if (!await IsAuthorizedAsync(services, caller, permission))
        {
            return NotAllowedMessage;
        }

        var (invoice, error) = await FindInvoiceAsync(services, Read(arguments, "invoice"), cancellationToken);

        if (invoice is null)
        {
            return error;
        }

        var (request, refusal) = await ReadActionAsync(services, arguments, invoice, action, "Nothing was previewed", cancellationToken);

        if (request is null)
        {
            return refusal;
        }

        var (token, expiresAt, tokenError) = await IssuePreviewTokenAsync(services, caller, request.Fingerprint, cancellationToken);

        if (token is null)
        {
            return tokenError;
        }

        invoice = request.Invoice;

        var owed = request.Ledger.Outstanding;
        var owedAfter = action == RecordPaymentAction ? owed - request.Amount : 0m;

        return Json(new
        {
            invoiceId = invoice.ItemId,
            number = invoice.Number,
            kind = ToCamel(invoice.Kind.ToString()),
            customer = invoice.BillTo?.Contact?.Name,
            currency = invoice.Currency,
            total = invoice.Totals?.Total ?? 0m,
            amountPaid = invoice.AmountPaid,
            amountCredited = invoice.AmountCredited,
            owedOnLedger = owed,
            action,
            amount = request.Amount,
            entireBalance = action == RecordPaymentAction ? request.EntireBalance : (bool?)null,
            method = request.Method,
            reference = request.Reference,
            receivedAt = await LocalTimeAsync(services, request.ReceivedUtc),
            after = new
            {
                amountDue = owedAfter,
                status = action switch
                {
                    VoidAction => ToCamel(nameof(InvoiceStatus.Void)),
                    WriteOffAction => ToCamel(nameof(InvoiceStatus.Uncollectible)),
                    _ => ToCamel(nameof(InvoiceStatus.Open)),
                },
                settlement = action == RecordPaymentAction
                    ? ToCamel((owedAfter <= 0m ? InvoiceSettlement.Paid : InvoiceSettlement.PartiallyPaid).ToString())
                    : null,
            },
            notes = GetNotes(invoice, action),
            previewToken = token,
            expiresAt,
            message = $"Nothing was changed. Show this to the user. To go ahead, call {ToolFor(action)} with the same invoice, amount, currency{(action == RecordPaymentAction ? ", entireBalance, method, reference, note, receivedAt, a new idempotencyKey" : " and a reason")} and this previewToken, before it expires.",
        });
    }

    private static string ToolFor(string action)
        => action switch
        {
            VoidAction => VoidInvoiceTool.TheName,
            WriteOffAction => WriteOffInvoiceTool.TheName,
            _ => RecordInvoicePaymentTool.TheName,
        };

    private static List<string> GetNotes(Invoice invoice, string action)
    {
        var notes = new List<string>();

        switch (action)
        {
            case VoidAction:
                notes.Add("A void can't be undone. The invoice keeps its number and nothing is owed on it any more.");

                if (invoice.Kind == InvoiceKind.DebitNote)
                {
                    notes.Add("This is a late-fee debit note: voiding it waives that fee.");
                }

                break;

            case WriteOffAction:
                notes.Add("What is still owed stops being collected. Staff can reinstate the invoice on its page.");
                break;

            default:
                notes.Add("No money moves through the site: this records money already received. The entry can't be removed from a chat, and the customer may be e-mailed a receipt.");
                break;
        }

        return notes;
    }
}
