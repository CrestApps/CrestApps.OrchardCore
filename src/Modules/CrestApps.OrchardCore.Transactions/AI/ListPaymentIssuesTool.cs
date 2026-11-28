using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Transactions.AI;

/// <summary>
/// Lists the money operations that failed and need a person, as Commerce › Payment issues does
/// (<see cref="IPaymentIssueStore.PageAsync"/>): open ones by default.
/// </summary>
internal sealed class ListPaymentIssuesTool : PaymentLedgerAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listPaymentIssues";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "status": {
          "type": "string",
          "enum": ["open", "resolved", "all"],
          "description": "Which issues to list; open by default."
        },
        "page": {
          "type": "integer",
          "description": "The page, from 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "Issues per page, 20 by default, at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists money operations that failed and need a person, with kind, message, error, what they relate to, provider reference and amount; open ones by default.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        PaymentIssueStatus? status;

        switch (ReadString(arguments, "status")?.ToLowerInvariant() ?? "open")
        {
            case "open":
                status = PaymentIssueStatus.Open;
                break;
            case "resolved":
                status = PaymentIssueStatus.Resolved;
                break;
            case "all":
                status = null;
                break;
            default:
                return "Nothing was listed: status must be open, resolved or all.";
        }

        var (page, pageSize) = ReadPaging(arguments);
        var result = await services.GetRequiredService<IPaymentIssueStore>().PageAsync(page, pageSize, status, cancellationToken);
        var items = new List<object>();

        foreach (var issue in result.Entries)
        {
            items.Add(new
            {
                id = issue.ItemId,
                at = await LocalTimeAsync(services, issue.CreatedUtc),
                kind = issue.Kind,
                message = issue.Message,
                error = issue.Error,
                referenceType = issue.ReferenceType,
                referenceId = issue.ReferenceId,
                paymentMethod = issue.ProviderKey,
                providerReference = issue.ProviderReference,
                amount = issue.Amount,
                currency = issue.Currency,
                status = ToCamel(issue.Status.ToString()),
                resolvedAt = await LocalTimeAsync(services, issue.ResolvedUtc),
                resolvedBy = issue.ResolvedBy,
                resolutionNote = issue.ResolutionNote,
            });
        }

        return Json(new
        {
            items,
            total = result.Count,
            page,
            pageSize,
            truncated = page * pageSize < result.Count,
        });
    }
}
