using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Invoicing.AI;

/// <summary>
/// Writes off what is still owed on an open invoice as uncollectible, through the Write off action of its page
/// (<see cref="IInvoiceCommandService.WriteOffAsync"/>, a write-off adjustment of what the transaction still owes). It
/// needs the token <c>previewInvoiceAction</c> issued for the same invoice and amount (the owner's review, F23); the
/// command is idempotent. Staff reinstate a written-off invoice on its page.
/// </summary>
internal sealed class WriteOffInvoiceTool : InvoiceActionToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "writeOffInvoice";

    private const string Refused = "Nothing was written off";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "invoice": { "type": "string", "description": "The invoice's number, such as INV-1001, or its id." },
            "amount": { "type": "number", "description": "Everything the ledger still has owed on the invoice, as previewInvoiceAction showed it." },
            "currency": { "type": "string", "description": "The invoice's currency, such as USD." },
            "reason": { "type": "string", "description": "Why it is written off, up to 500 characters; kept on the invoice." },
            "previewToken": { "type": "string", "description": "The previewToken previewInvoiceAction returned for this invoice with the writeOff action and this amount. It works once and expires 10 minutes after the preview." }
          },
          "required": ["invoice", "amount", "currency", "reason", "previewToken"],
          "additionalProperties": false
        }
        """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Writes off everything still owed on an open invoice as uncollectible, with a reason; staff can reinstate it on the invoice's page. Call previewInvoiceAction with the writeOff action first and show the result to the user: this tool needs the previewToken it returns.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, caller, InvoicingPermissions.VoidInvoices))
        {
            return NotAllowedMessage;
        }

        var (reason, reasonError) = ReadReason(arguments, Refused, "written off");

        if (reason is null)
        {
            return reasonError;
        }

        var (invoice, error) = await FindInvoiceAsync(services, Read(arguments, "invoice"), cancellationToken);

        if (invoice is null)
        {
            return error;
        }

        if (invoice.Status == InvoiceStatus.Uncollectible)
        {
            return $"Nothing was written off: invoice {invoice.Number} is already written off.";
        }

        var (request, refusal) = await ReadActionAsync(services, arguments, invoice, WriteOffAction, Refused, cancellationToken);

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
            result = await services.GetRequiredService<IInvoiceCommandService>().WriteOffAsync(request.Invoice.ItemId, reason, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MarkSessionClosed(services);

            return "Writing off the invoice stopped with an error, and whether it was written off is unknown. Open the invoice's page before trying again.";
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
            writtenOff = request.Amount,
            currency = saved.Currency,
            alreadyWrittenOff = result.AlreadyDone ? true : (bool?)null,
            adminUrl = InvoiceAdminPath(services, saved.ItemId),
            message = $"{Format(request.Amount, saved.Currency)} owed on invoice {saved.Number} was written off. Staff can reinstate it on the invoice's page.",
        });
    }
}
