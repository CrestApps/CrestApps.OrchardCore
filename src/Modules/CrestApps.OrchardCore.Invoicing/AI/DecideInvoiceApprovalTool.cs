using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Invoicing.AI;

/// <summary>
/// Approves an invoice waiting for approval, which issues it, or rejects it with a reason, which makes it a draft again,
/// through the Approve and Reject actions of its page (<see cref="IInvoiceCommandService.ApproveAsync"/> and
/// <see cref="IInvoiceCommandService.RejectAsync"/>). The approver is always the caller, never an argument, so the rule
/// that whoever submitted an invoice can't approve it holds. Approving a draft that charges the customer's saved card or
/// that revises another invoice is refused, as <c>issueInvoice</c> refuses issuing one.
/// </summary>
internal sealed class DecideInvoiceApprovalTool : InvoiceAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "decideInvoiceApproval";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "invoice": { "type": "string", "description": "The id of the invoice waiting for approval, from findInvoices with status pendingApproval." },
            "decision": { "type": "string", "enum": ["approve", "reject"], "description": "approve issues the invoice; reject makes it a draft again." },
            "reason": { "type": "string", "description": "Why it is rejected, up to 500 characters; required to reject." }
          },
          "required": ["invoice", "decision"],
          "additionalProperties": false
        }
        """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "For invoices waiting for approval only (find them with findInvoices and status pendingApproval): approves one, which issues it (it gets its number and what it totals is owed; this can't be undone), or rejects it with a reason, which makes it a draft again. Whoever submitted the invoice can't approve it. Other approval requests, such as pay runs, are decided with decideApprovalRequest, not this tool.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, caller, InvoicingPermissions.ApproveInvoices))
        {
            return NotAllowedMessage;
        }

        var decision = Read(arguments, "decision")?.ToLowerInvariant();

        if (decision is not ("approve" or "reject"))
        {
            return "Nothing was decided: give the decision, approve or reject.";
        }

        var approve = decision == "approve";
        var reason = Read(arguments, "reason");

        if (!approve && reason is null)
        {
            return "Nothing was rejected: say why the invoice is rejected.";
        }

        if (reason?.Length > MaxReasonLength)
        {
            return $"Nothing was decided: the reason is longer than {MaxReasonLength} characters.";
        }

        var (invoice, error) = await FindInvoiceAsync(services, Read(arguments, "invoice"), cancellationToken);

        if (invoice is null)
        {
            return error;
        }

        if (invoice.Status != InvoiceStatus.PendingApproval)
        {
            return $"Nothing was decided: the invoice is {ToCamel(invoice.Status.ToString())}, not waiting for approval.";
        }

        if (approve && invoice.AutoCharge)
        {
            return "Nothing was approved: approving this invoice issues it, and it charges the customer's saved card when it is issued. Approve it on the invoice's page.";
        }

        if (approve && !string.IsNullOrEmpty(invoice.RevisionOfId))
        {
            return "Nothing was approved: this invoice revises another one, and approving it issues it, which voids the invoice it revises. Approve it on the invoice's page.";
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlier)
        {
            return earlier;
        }

        var commands = services.GetRequiredService<IInvoiceCommandService>();
        InvoiceCommandResult result;

        try
        {
            result = approve
                ? await commands.ApproveAsync(invoice.ItemId, ActorName(caller), cancellationToken)
                : await commands.RejectAsync(invoice.ItemId, ActorName(caller), reason, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MarkSessionClosed(services);

            return "The decision stopped with an error. Open the invoice's page to see where it stands.";
        }

        NoteCommand(services, result);

        if (!result.Succeeded)
        {
            return $"Nothing was {(approve ? "approved" : "rejected")}: {string.Join(" ", result.Errors)}";
        }

        var saved = result.Invoice ?? invoice;

        return Json(new
        {
            id = saved.ItemId,
            number = saved.Number,
            status = ToCamel(saved.Status.ToString()),
            currency = saved.Currency,
            total = saved.Totals?.Total ?? 0m,
            amountDue = saved.AmountDue,
            issueDate = Date(saved.IssueDate),
            dueDate = Date(saved.DueDate),
            adminUrl = InvoiceAdminPath(services, saved.ItemId),
            message = approve
                ? $"Invoice {saved.Number} was approved and issued."
                : "The invoice was rejected and is a draft again.",
        });
    }
}
