using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Transactions.AI;

/// <summary>
/// Cancels a transaction through the Cancel action of its page (<see cref="ITransactionCommandService.CancelAsync"/>,
/// which locks, checks and commits it): it is no longer collected or reminded, and it cannot be reopened. It never
/// refunds.
/// </summary>
internal sealed class CancelTransactionTool : TransactionAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "cancelTransaction";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "transactionId": {
          "type": "string",
          "description": "The transaction's id, from listTransactions."
        },
        "reason": {
          "type": "string",
          "description": "Why it is canceled, kept on the timeline."
        }
      },
      "required": ["transactionId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Cancels an outstanding transaction so it is no longer collected or reminded, with an optional reason. It cannot be reopened and it refunds nothing.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (transaction, error) = await FindTransactionAsync(services, ReadString(arguments, "transactionId"), cancellationToken);

        if (transaction is null)
        {
            return error;
        }

        var result = await services.GetRequiredService<ITransactionCommandService>().CancelAsync(transaction.ItemId, ReadString(arguments, "reason"), cancellationToken);

        if (!result.Succeeded)
        {
            return $"Nothing was canceled: {string.Join(" ", result.Errors)}";
        }

        var answer = await DescribeAsync(services, result.Transaction);
        answer["message"] = "The transaction was canceled. Nothing was refunded.";

        return Json(answer);
    }
}
