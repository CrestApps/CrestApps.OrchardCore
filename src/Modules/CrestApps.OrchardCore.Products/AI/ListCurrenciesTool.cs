using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Products.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Products.AI;

/// <summary>
/// Lists the currencies of Commerce › Currencies (<see cref="INamedCatalog{T}"/> of <see cref="CurrencyEntry"/>), with
/// the changes the currency tools made earlier in the same chat message. It needs <c>ManageCurrencies</c>, as the page
/// does.
/// </summary>
internal sealed class ListCurrenciesTool : ProductsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listCurrencies";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "query": {
          "type": "string",
          "description": "Text to find in the ISO code or the display name, such as EUR or euro."
        },
        "page": {
          "type": "integer",
          "description": "The page, from 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "Currencies per page, 20 by default and at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the currencies the store can price products in, with ISO code and display name, optionally filtered by text.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ProductsConstants.Permissions.ManageCurrencies;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var stored = await services.GetRequiredService<INamedCatalog<CurrencyEntry>>().GetAllAsync(cancellationToken);
        var scope = services.GetService<CurrencyToolScope>();
        var currencies = (scope?.Apply(stored) ?? stored).Where(entry => entry?.Name is not null);
        var query = ReadString(arguments, "query");

        if (query is not null)
        {
            currencies = currencies.Where(entry =>
                entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (entry.DisplayName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var all = currencies.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var (page, pageSize) = ReadPaging(arguments);

        return Json(new
        {
            items = all.Skip((page - 1) * pageSize).Take(pageSize).Select(entry => new
            {
                code = entry.Name,
                displayName = entry.DisplayName,
            }).ToArray(),
            total = all.Length,
            page,
            pageSize,
            truncated = page * pageSize < all.Length,
        });
    }
}
