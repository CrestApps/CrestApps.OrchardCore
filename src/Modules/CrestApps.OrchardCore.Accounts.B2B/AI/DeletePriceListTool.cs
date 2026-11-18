using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Accounts.B2B.AI;

/// <summary>
/// Deletes a price list as the Price lists page does (<see cref="PriceListRules.DeleteAsync"/>): the accounts on it are
/// taken off it, so they pay the store's prices, and the list is deleted. It needs <c>ManageB2BAccounts</c> and asks for
/// approval.
/// </summary>
internal sealed class DeletePriceListTool : B2BAccountsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deletePriceList";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "priceListId": {
          "type": "string",
          "description": "The id of the price list to delete, from listPriceLists."
        }
      },
      "required": ["priceListId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a price list. Accounts that used it fall back to normal prices.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var priceListId = ReadString(arguments, "priceListId");
        var store = services.GetRequiredService<IPriceListStore>();
        var list = priceListId is null ? null : await store.FindByIdAsync(priceListId, cancellationToken);

        if (list is null)
        {
            return $"Nothing was deleted: there is no price list with the id '{priceListId}'. Use listPriceLists to find it.";
        }

        var accounts = await PriceListRules.DeleteAsync(store, services.GetRequiredService<IB2BAccountStore>(), list, cancellationToken);

        return Json(new
        {
            deleted = true,
            id = list.ItemId,
            name = list.Name,
            accountsMovedToStorePrices = accounts.Select(account => new { contentItemId = account.AccountId, account = account.Name }).ToList(),
            message = accounts.Count == 0
                ? "The price list was deleted. No account was on it."
                : $"The price list was deleted. The {accounts.Count} account(s) on it now pay the store's prices.",
        });
    }
}
