using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Transactions.AI;

/// <summary>
/// Marks a payment issue as resolved with a note, through the Resolve action of Commerce › Payment issues
/// (<see cref="TransactionAdminActions.ResolveIssueAsync"/>). It only closes the issue; it performs no payment
/// operation. Committed at once.
/// </summary>
internal sealed class ResolvePaymentIssueTool : PaymentLedgerAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "resolvePaymentIssue";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "issueId": {
          "type": "string",
          "description": "The issue's id, from listPaymentIssues."
        },
        "note": {
          "type": "string",
          "description": "What was done, such as the reference of a refund made by hand."
        }
      },
      "required": ["issueId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Marks a payment issue as resolved with a note saying what was done, such as the reference of a manual refund. It only closes the issue; it performs no payment operation.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var issueId = ReadString(arguments, "issueId");
        var issueStore = services.GetRequiredService<IPaymentIssueStore>();
        var issue = issueId is null ? null : await issueStore.FindByIdAsync(issueId, cancellationToken);

        if (issue is null)
        {
            return $"Nothing was resolved: no payment issue has the id '{issueId}'. Use listPaymentIssues to find it.";
        }

        var utcNow = services.GetRequiredService<IClock>().UtcNow;

        if (!await TransactionAdminActions.ResolveIssueAsync(issueStore, issue, ReadString(arguments, "note"), caller.Identity?.Name, utcNow, cancellationToken))
        {
            return "Nothing was resolved: the payment issue is already resolved.";
        }

        var session = services.GetRequiredService<ISession>();

        try
        {
            await session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            await session.CancelAsync();

            return "Nothing was resolved: the issue changed at the same moment. Read it again with listPaymentIssues and retry in your next message.";
        }

        return Json(new
        {
            id = issue.ItemId,
            status = "resolved",
            resolvedBy = issue.ResolvedBy,
            note = issue.ResolutionNote,
            message = "The payment issue was marked as resolved. No payment operation was performed.",
        });
    }
}
