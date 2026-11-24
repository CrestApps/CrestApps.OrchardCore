using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Invoicing.AI;

/// <summary>
/// Issues the draft credit note <c>previewCreditNote</c> prepared, through
/// <see cref="ICreditNoteCommandService.IssueAsync"/> (the credit note's <b>Issue</b> button), which takes the invoice's lock,
/// works the draft out again, checks it and numbers it, then lowers what the invoice owes, refunds through the payment
/// provider or adds the customer's credit, keyed by the credit note so the money never moves twice.
/// </summary>
/// <remarks>
/// Stricter than the page, as the owner asked (F23): the total, currency and disposition are repeated, so the approval
/// shows what moves, and must be the stored draft's; the preview's token must match the stored draft and the ledger's limit
/// for its disposition, so a draft changed on its page after the preview, or a payment recorded meanwhile, is refused.
/// There is no idempotency key: the issue is idempotent per credit note (a second call finds it issued and moves nothing),
/// and a new credit needs a new draft, preview and approval.
/// </remarks>
internal sealed class IssueCreditNoteTool : CreditNoteAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "issueCreditNote";

    private const string Refused = "Nothing was issued";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "creditNoteId": {
          "type": "string",
          "description": "The draft credit note's id, from previewCreditNote."
        },
        "total": {
          "type": "number",
          "description": "The credit note's total, tax included, exactly as previewCreditNote returned it."
        },
        "currency": {
          "type": "string",
          "description": "The credit note's currency, such as USD."
        },
        "disposition": {
          "type": "string",
          "enum": ["reduceBalance", "refund", "customerCredit"],
          "description": "The disposition previewCreditNote returned: lower what the invoice owes, refund what was paid, or keep it as the customer's credit."
        },
        "previewToken": {
          "type": "string",
          "description": "The previewToken previewCreditNote returned for this draft. It works once and expires 10 minutes after the preview."
        }
      },
      "required": ["creditNoteId", "total", "currency", "disposition", "previewToken"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Issues the draft credit note previewCreditNote prepared: numbers it and lowers what the invoice owes, refunds the payment through the payment provider, or adds the amount to the customer's credit. Needs the previewToken, and the credit note's total, currency and disposition exactly as previewed; call previewCreditNote first and show its result to the user. Cannot be undone.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, caller, InvoicingPermissions.ManageCreditNotes))
        {
            return NotAllowedMessage;
        }

        var id = Read(arguments, "creditNoteId");
        var draft = id is null ? null : await services.GetRequiredService<ICreditNoteStore>().FindByIdAsync(id, cancellationToken);

        if (draft is null)
        {
            return $"{Refused}: no credit note has the id '{id}'. Prepare one with {PreviewToolName}.";
        }

        if (draft.Status == CreditNoteStatus.Issued)
        {
            return Json(new
            {
                creditNoteId = draft.ItemId,
                number = draft.Number,
                invoiceNumber = draft.InvoiceNumber,
                total = draft.Totals?.Total ?? 0m,
                currency = draft.Currency,
                disposition = ToCamel(draft.Disposition.ToString()),
                alreadyDone = true,
                adminUrl = CreditNoteAdminPath(services, draft.ItemId),
                message = $"{Refused} again: credit note {draft.Number} was issued already, and nothing moved this time.",
            });
        }

        if (draft.Status != CreditNoteStatus.Draft)
        {
            return $"{Refused}: credit note {draft.Number} is {ToCamel(draft.Status.ToString())}; only a draft can be issued.";
        }

        if (!TryReadDecimal(arguments, "total", out var total) || total is null)
        {
            return $"{Refused}: give the credit note's total, as {PreviewToolName} returned it.";
        }

        var draftTotal = draft.Totals?.Total ?? 0m;

        if (!string.Equals(Read(arguments, "currency"), draft.Currency, StringComparison.OrdinalIgnoreCase) ||
            ParseDisposition(Read(arguments, "disposition")) != draft.Disposition ||
            total.Value != draftTotal)
        {
            return $"{Refused}: the draft credit note is {Format(draftTotal, draft.Currency)} to {ToCamel(draft.Disposition.ToString())}, not what was given. It may have been changed on its page since the preview: call {PreviewToolName} again, show the result to the user, and use its total, currency, disposition and token.";
        }

        var (invoice, notFound) = await FindInvoiceAsync(services, draft.InvoiceId, cancellationToken);

        if (invoice is null)
        {
            return $"{Refused}: {notFound}";
        }

        var ledger = await ReadLedgerAsync(services, invoice, Refused, cancellationToken);

        if (ledger.Error is not null)
        {
            return ledger.Error;
        }

        var limit = CreditNoteRules.GetDispositionLimit(draft.Disposition, ledger.Transaction);

        // Earlier work first, as the void and write-off tools: a message that can save nothing more leaves the token unused.
        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlier)
        {
            return earlier;
        }

        if (await TakePreviewTokenAsync(services, caller, Read(arguments, "previewToken"), CreditNoteFingerprint(draft, limit), Refused, PreviewToolName, cancellationToken) is { } tokenRefused)
        {
            return tokenRefused;
        }

        CreditNoteCommandResult result;

        try
        {
            result = await services.GetRequiredService<ICreditNoteCommandService>().IssueAsync(draft.ItemId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MarkSessionClosed(services);

            return $"Issuing credit note {draft.ItemId} stopped with an error, and whether money moved is unknown. Do not retry; check the credit note's and the invoice's pages.";
        }

        if (!result.Succeeded || result.CreditNote is null)
        {
            // The service discards the session's work when a refusal comes after numbering.
            MarkSessionClosed(services);

            return Json(new
            {
                creditNoteId = draft.ItemId,
                invoiceNumber = invoice.Number,
                errors = result.Errors,
                adminUrl = CreditNoteAdminPath(services, draft.ItemId),
                message = $"{Refused}; read the errors. The draft stays on the invoice and the previewToken is used up: to try again, call {PreviewToolName} with replaceDraft set to {draft.ItemId}.",
            });
        }

        var issued = result.CreditNote;
        var issuedTotal = issued.Totals?.Total ?? 0m;

        return Json(new
        {
            creditNoteId = issued.ItemId,
            number = issued.Number,
            invoiceId = invoice.ItemId,
            invoiceNumber = invoice.Number,
            total = issuedTotal,
            previewedTotal = issuedTotal != draftTotal ? draftTotal : (decimal?)null,
            currency = issued.Currency,
            disposition = ToCamel(issued.Disposition.ToString()),
            applied = issued.AmountApplied,
            refunded = issued.AmountRefunded,
            credited = issued.AmountCredited,
            invoiceAmountDue = result.Invoice?.AmountDue,
            alreadyDone = result.AlreadyDone,
            adminUrl = CreditNoteAdminPath(services, issued.ItemId),
            message = result.AlreadyDone
                ? $"Credit note {issued.Number} was issued already; nothing moved this time."
                : $"Credit note {issued.Number} was issued for {Format(issuedTotal, issued.Currency)} against invoice {invoice.Number}.{(issuedTotal != draftTotal ? $" The total was worked out again on issue and differs from the preview ({Format(draftTotal, issued.Currency)})." : string.Empty)}",
        });
    }
}
