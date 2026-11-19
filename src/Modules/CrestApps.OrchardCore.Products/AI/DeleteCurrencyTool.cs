using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Products.Models;
using CrestApps.OrchardCore.Products.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Products.AI;

/// <summary>
/// Deletes a currency as the Currencies page does: refused while products are priced in it
/// (<see cref="CurrencyUsageService.GetProductsUsingAsync"/>, which the page calls and the catalog does not), and the
/// products are named. It needs <c>ManageCurrencies</c> and asks for approval.
/// </summary>
internal sealed class DeleteCurrencyTool : ProductsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteCurrency";

    private const int MaxNamed = 10;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "code": {
          "type": "string",
          "description": "The currency's three-letter ISO 4217 code, from listCurrencies."
        }
      },
      "required": ["code"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a currency. It is refused while any product is priced in it; the products are named so their prices can be changed first.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ProductsConstants.Permissions.ManageCurrencies;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var code = CurrencyCodeUtility.Normalize(ReadString(arguments, "code"));
        var scope = services.GetService<CurrencyToolScope>();

        if (scope?.WasChanged(code) == true)
        {
            return $"Nothing was deleted: {code} was already changed in this message. Delete it in your next message.";
        }

        var manager = services.GetRequiredService<INamedCatalogManager<CurrencyEntry>>();
        var entry = string.IsNullOrEmpty(code) ? null : await manager.FindByNameAsync(code, cancellationToken);

        if (entry is null)
        {
            return $"Nothing was deleted: there is no currency '{code}'. Use listCurrencies for the codes.";
        }

        var products = await services.GetRequiredService<CurrencyUsageService>().GetProductsUsingAsync(entry.Name);

        if (products.Count > 0)
        {
            var named = string.Join(", ", products.Take(MaxNamed));

            return products.Count > MaxNamed
                ? $"Nothing was deleted: products are priced in {entry.Name}: {named} and {products.Count - MaxNamed} more. Change their prices to another currency first."
                : $"Nothing was deleted: products are priced in {entry.Name}: {named}. Change their prices to another currency first.";
        }

        if (!await manager.DeleteAsync(entry, cancellationToken))
        {
            return $"Nothing was deleted: the currency {entry.Name} could not be deleted.";
        }

        scope?.RecordDeleted(entry.Name);

        return Json(new
        {
            code = entry.Name,
            deleted = true,
            message = $"The currency {entry.Name} was deleted.",
        });
    }
}
