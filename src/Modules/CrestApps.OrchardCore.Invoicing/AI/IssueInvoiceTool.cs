using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Invoicing.AI;

/// <summary>
/// Issues a draft invoice through the Issue action of its page (<see cref="IInvoiceCommandService.IssueAsync"/>), which
/// numbers it and posts what it totals to the ledger, or submits it for approval as the Submit action does when its total
/// needs approval. With <c>send</c> it then e-mails the issued invoice as the Send dialog does
/// (<see cref="IInvoiceSender.SendAsync"/>), to the bill-to's own address and copy addresses only: the tool takes no
/// recipient. It refuses a draft marked to charge the customer's saved card (issuing would charge it at once, with no
/// preview) and a revision (issuing it voids the invoice it revises, which the void tools preview first): both are issued
/// on the page.
/// </summary>
internal sealed class IssueInvoiceTool : InvoiceAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "issueInvoice";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "invoice": { "type": "string", "description": "The draft's id, from draftInvoice or findInvoices with status draft." },
            "send": { "type": "boolean", "description": "Also e-mail the issued invoice, with its PDF and pay link, to the customer's billing address and copy addresses. False when not given." },
            "submitForApproval": { "type": "boolean", "description": "When the draft's total needs approval, submit it for approval instead of issuing it; someone else then approves it, which issues it. False when not given." }
          },
          "required": ["invoice"],
          "additionalProperties": false
        }
        """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Issues a draft invoice: gives it its number and posts what it totals to the ledger as owed. It can't be undone, only voided. With send it also e-mails the invoice to the customer's billing address. A draft whose total needs approval is not issued: call again with submitForApproval to submit it. Drafts that charge the customer's saved card or that revise another invoice are issued on the invoice's page.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var send = ReadBool(arguments, "send") == true;
        var submit = ReadBool(arguments, "submitForApproval") == true;
        var canIssue = await IsAuthorizedAsync(services, caller, InvoicingPermissions.IssueInvoices);
        var canManage = await IsAuthorizedAsync(services, caller, InvoicingPermissions.ManageInvoices);

        // As the page: issuing needs Issue invoices, sending also Manage invoices, and submitting either of them.
        if (submit ? (!canIssue && !canManage) : (!canIssue || (send && !canManage)))
        {
            return NotAllowedMessage;
        }

        var (invoice, error) = await FindInvoiceAsync(services, Read(arguments, "invoice"), cancellationToken);

        if (invoice is null)
        {
            return error;
        }

        if (invoice.Status == InvoiceStatus.PendingApproval)
        {
            return "Nothing was issued: the invoice is waiting for approval and is issued when someone approves it (decideInvoiceApproval).";
        }

        if (invoice.Status != InvoiceStatus.Draft)
        {
            return $"Nothing was issued: invoice {invoice.Number} is already issued ({ToCamel(invoice.Status.ToString())}).";
        }

        if (invoice.AutoCharge)
        {
            return "Nothing was issued: this draft charges the customer's saved card when it is issued, so issuing it moves money. Issue it from the invoice's page.";
        }

        if (!string.IsNullOrEmpty(invoice.RevisionOfId))
        {
            var revised = await services.GetRequiredService<IInvoiceManager>().FindByIdAsync(invoice.RevisionOfId, cancellationToken);

            return $"Nothing was issued: this draft revises invoice {revised?.Number ?? invoice.RevisionOfId}, and issuing it voids that invoice. Issue it from its page.";
        }

        // Decided before the command, as the invoice's page decides which button it shows: the issue command refuses a
        // draft that needs approval by discarding the session's work, after which nothing more can be saved in this message.
        if (!submit &&
            InvoiceApprovalRules.Requires(invoice, await services.GetRequiredService<ISiteService>().GetSettingsAsync<InvoicingSettings>()))
        {
            return Json(new
            {
                id = invoice.ItemId,
                needsApproval = true,
                message = "Nothing was issued: the invoice's total needs approval. Call issueInvoice again with submitForApproval true to submit it; someone else then approves it.",
            });
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlier)
        {
            return earlier;
        }

        var commands = services.GetRequiredService<IInvoiceCommandService>();
        InvoiceCommandResult result;

        try
        {
            result = submit
                ? await commands.SubmitForApprovalAsync(invoice.ItemId, ActorName(caller), cancellationToken)
                : await commands.IssueAsync(invoice.ItemId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MarkSessionClosed(services);

            return "Issuing the invoice stopped with an error. Open the invoice's page to see whether it was issued.";
        }

        NoteCommand(services, result);

        if (result.NeedsApproval)
        {
            return Json(new
            {
                id = invoice.ItemId,
                needsApproval = true,
                message = "Nothing was issued: worked out again for issuing, the invoice's total needs approval. In your next message, call issueInvoice again with submitForApproval true to submit it; someone else then approves it.",
            });
        }

        if (!result.Succeeded)
        {
            return $"Nothing was {(submit ? "submitted" : "issued")}: {string.Join(" ", result.Errors)}";
        }

        var saved = result.Invoice ?? invoice;

        if (submit)
        {
            return Json(new
            {
                id = saved.ItemId,
                status = ToCamel(saved.Status.ToString()),
                submittedForApproval = true,
                total = saved.Totals?.Total ?? 0m,
                currency = saved.Currency,
                adminUrl = InvoiceAdminPath(services, saved.ItemId),
                message = "The invoice was submitted for approval. It is issued when someone else approves it.",
            });
        }

        string sentTo = null;
        IList<string> sendErrors = null;

        if (send)
        {
            var sent = await services.GetRequiredService<IInvoiceSender>().SendAsync(new InvoiceSendRequest { InvoiceId = saved.ItemId }, cancellationToken);

            if (sent.Succeeded)
            {
                saved = sent.Invoice ?? saved;
                sentTo = saved.BillTo?.Contact?.Email;
            }
            else
            {
                sendErrors = sent.Errors;
            }
        }

        return Json(new
        {
            id = saved.ItemId,
            number = saved.Number,
            status = ToCamel(saved.Status.ToString()),
            issueDate = Date(saved.IssueDate),
            dueDate = Date(saved.DueDate),
            currency = saved.Currency,
            total = saved.Totals?.Total ?? 0m,
            amountDue = saved.AmountDue,
            alreadyIssued = result.AlreadyDone ? true : (bool?)null,
            sent = send ? sendErrors is null : (bool?)null,
            sentTo,
            sendErrors,
            adminUrl = InvoiceAdminPath(services, saved.ItemId),
            message = sendErrors is not null
                ? $"Invoice {saved.Number} was issued but not sent: {string.Join(" ", sendErrors)}"
                : send
                    ? $"Invoice {saved.Number} was issued and e-mailed to {sentTo ?? "the customer"}."
                    : $"Invoice {saved.Number} was issued.",
        });
    }
}
