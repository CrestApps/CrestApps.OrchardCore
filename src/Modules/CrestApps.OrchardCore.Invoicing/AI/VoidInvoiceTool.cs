using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Invoicing.AI;

/// <summary>
/// Voids an open invoice nothing was paid or credited on, through the Void action of its page
/// (<see cref="IInvoiceCommandService.VoidAsync"/>, which cancels its transaction under the ledger's lock and keeps its
/// number). It waives a late fee when given the fee's debit note. It needs the token <c>previewInvoiceAction</c> issued for
/// the same invoice and amount (the owner's review, F23); the command is idempotent, so a repeat reports that the invoice
/// is already void.
/// </summary>
internal sealed class VoidInvoiceTool : InvoiceActionToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "voidInvoice";

    private const string Refused = "Nothing was voided";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "invoice": { "type": "string", "description": "The invoice's number, such as INV-1001, or its id; a late fee's debit note to waive the fee." },
            "amount": { "type": "number", "description": "Everything the ledger still has owed on the invoice, as previewInvoiceAction showed it (0 for a zero-total invoice)." },
            "currency": { "type": "string", "description": "The invoice's currency, such as USD." },
            "reason": { "type": "string", "description": "Why the invoice is voided, up to 500 characters; kept on the invoice." },
            "previewToken": { "type": "string", "description": "The previewToken previewInvoiceAction returned for this invoice with the void action and this amount. It works once and expires 10 minutes after the preview." }
          },
          "required": ["invoice", "amount", "currency", "reason", "previewToken"],
          "additionalProperties": false
        }
        """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Voids an open invoice nothing was paid or credited on: what is owed is canceled on the ledger and the invoice keeps its number. It can't be undone. Given a late fee's debit note, it waives that fee. Call previewInvoiceAction with the void action first and show the result to the user: this tool needs the previewToken it returns.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, caller, InvoicingPermissions.VoidInvoices))
        {
            return NotAllowedMessage;
        }

        var (reason, reasonError) = ReadReason(arguments, Refused, "voided");

        if (reason is null)
        {
            return reasonError;
        }

        var (invoice, error) = await FindInvoiceAsync(services, Read(arguments, "invoice"), cancellationToken);

        if (invoice is null)
        {
            return error;
        }

        if (invoice.Status == InvoiceStatus.Void)
        {
            return $"Nothing was voided: invoice {invoice.Number} is already void.";
        }

        var (request, refusal) = await ReadActionAsync(services, arguments, invoice, VoidAction, Refused, cancellationToken);

        if (request is null)
        {
            return refusal;
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlier)
        {
            return earlier;
        }

        if (await TakePreviewTokenAsync(services, caller, Read(arguments, "previewToken"), request.Fingerprint, Refused, PreviewToolName, cancellationToken) is { } tokenError)
        {
            return tokenError;
        }

        InvoiceCommandResult result;

        try
        {
            result = await services.GetRequiredService<IInvoiceCommandService>().VoidAsync(request.Invoice.ItemId, reason, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MarkSessionClosed(services);

            return "Voiding the invoice stopped with an error, and whether it was voided is unknown. Open the invoice's page before trying again.";
        }

        NoteCommand(services, result);

        if (!result.Succeeded)
        {
            return $"{Refused}: {string.Join(" ", result.Errors)}";
        }

        var saved = result.Invoice ?? request.Invoice;

        return Json(new
        {
            id = saved.ItemId,
            number = saved.Number,
            status = ToCamel(saved.Status.ToString()),
            amountCanceled = request.Amount,
            currency = saved.Currency,
            voidReason = saved.VoidReason,
            alreadyVoid = result.AlreadyDone ? true : (bool?)null,
            adminUrl = InvoiceAdminPath(services, saved.ItemId),
            message = result.AlreadyDone
                ? $"Invoice {saved.Number} was already void; nothing changed."
                : $"Invoice {saved.Number} was voided: {Format(request.Amount, saved.Currency)} is no longer owed.",
        });
    }
}
