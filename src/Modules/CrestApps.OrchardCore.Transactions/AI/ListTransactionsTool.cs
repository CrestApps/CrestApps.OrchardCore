using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Transactions.ViewModels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Transactions.AI;

/// <summary>
/// Lists transactions as Commerce › Transactions does (<see cref="ITransactionManager.PageAsync"/>, with the page's
/// status filter, <see cref="TransactionStatusFilterExtensions.ApplyTo"/>, and its search and source).
/// </summary>
internal sealed class ListTransactionsTool : TransactionAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listTransactions";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Text to find in a transaction's title, invoice number or owner."
        },
        "status": {
          "type": "string",
          "enum": ["any", "outstanding", "pending", "partiallyPaid", "paid", "canceled", "failed", "abandoned", "refunded"],
          "description": "Which transactions to list; any by default. outstanding means anything still owed."
        },
        "source": {
          "type": "string",
          "description": "The source name, such as the module that created the transaction."
        },
        "page": {
          "type": "integer",
          "description": "The page, from 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "Transactions per page, 20 by default, at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists transactions (what customers owe or paid) with title, source, status, total, paid, outstanding, due date and invoice number. Filters by text, status (outstanding, pending, partially paid, paid, canceled, failed, abandoned, refunded) and source, with paging.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var status = ReadString(arguments, "status") ?? "any";
        TransactionStatusFilter filter;

        if (string.Equals(status, "any", StringComparison.OrdinalIgnoreCase))
        {
            filter = TransactionStatusFilter.All;
        }
        else if (!Enum.TryParse(status, ignoreCase: true, out filter) || filter == TransactionStatusFilter.All || !Enum.IsDefined(filter))
        {
            return "Nothing was listed: status must be any, outstanding, pending, partiallyPaid, paid, canceled, failed, abandoned or refunded.";
        }

        var query = new TransactionQuery
        {
            Search = ReadString(arguments, "search"),
            Source = ReadString(arguments, "source"),
        };

        filter.ApplyTo(query);

        var (page, pageSize) = ReadPaging(arguments);
        var result = await services.GetRequiredService<ITransactionManager>().PageAsync(page, pageSize, query, cancellationToken);
        var items = new List<object>();

        foreach (var transaction in result.Entries)
        {
            items.Add(await DescribeAsync(services, transaction));
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
