using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Products.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Products.AI;

/// <summary>
/// Adds a currency, or renames one, as the Currencies editor does: the code is a three-letter ISO 4217 code and the
/// display name is required (both checked by the currency handler through <c>ValidateAsync</c>), a new code must not
/// exist yet (the editor's check), and an existing currency's code never changes, because products are priced in it.
/// It needs <c>ManageCurrencies</c>.
/// </summary>
internal sealed class SaveCurrencyTool : ProductsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveCurrency";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "code": {
          "type": "string",
          "description": "The three-letter ISO 4217 code, such as EUR. An existing currency is found by it and renamed; its code never changes."
        },
        "displayName": {
          "type": "string",
          "description": "The name shown for the currency, such as Euro."
        }
      },
      "required": ["code", "displayName"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Adds a currency by three-letter ISO 4217 code and display name, or renames an existing one.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ProductsConstants.Permissions.ManageCurrencies;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var code = CurrencyCodeUtility.Normalize(ReadString(arguments, "code"));
        var displayName = ReadString(arguments, "displayName");

        if (string.IsNullOrEmpty(code) || !CurrencyCodeUtility.IsValid(code))
        {
            return "Nothing was saved: the currency code must be a three-letter ISO 4217 code, such as EUR.";
        }

        if (displayName is null)
        {
            return "Nothing was saved: give the currency a display name, such as Euro.";
        }

        var scope = services.GetService<CurrencyToolScope>();

        if (scope?.WasChanged(code) == true)
        {
            return $"Nothing was saved: {code} was already changed in this message. Change it again in your next message.";
        }

        var manager = services.GetRequiredService<INamedCatalogManager<CurrencyEntry>>();
        var existing = await manager.FindByNameAsync(code, cancellationToken);
        var entry = existing?.Clone() ?? await manager.NewAsync(cancellationToken: cancellationToken);

        entry.Name = existing?.Name ?? code;
        entry.DisplayName = displayName;

        var validation = await manager.ValidateAsync(entry, cancellationToken);

        if (!validation.Succeeded)
        {
            return $"Nothing was saved: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))}";
        }

        if (existing is null)
        {
            await manager.CreateAsync(entry, cancellationToken);
        }
        else
        {
            await manager.UpdateAsync(entry, cancellationToken: cancellationToken);
        }

        scope?.RecordSaved(entry);

        return Json(new
        {
            code = entry.Name,
            displayName = entry.DisplayName,
            created = existing is null,
            message = existing is null
                ? $"The currency {entry.Name} was added; products can be priced in it."
                : $"The currency {entry.Name} is now called {entry.DisplayName}.",
        });
    }
}
