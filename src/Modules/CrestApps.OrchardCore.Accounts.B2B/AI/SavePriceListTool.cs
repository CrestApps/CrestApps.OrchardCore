using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Checkout.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Accounts.B2B.AI;

/// <summary>
/// Creates or updates a price list with the Price lists editor's rules (<see cref="PriceListRules"/>): a name, a
/// percentage off from 0 to 100, prices by SKU with a SKU of at most 100 characters and a price of 0 or more, a later price
/// for the same SKU replacing an earlier one, and at most 5,000 prices. The prices are in the store's currency, as the
/// editor saves them. Only the fields given change; prices given replace the stored ones. It needs
/// <c>ManageB2BAccounts</c>.
/// </summary>
internal sealed class SavePriceListTool : B2BAccountsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "savePriceList";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "priceListId": {
          "type": "string",
          "description": "The id of the price list to change, from listPriceLists. Leave it out to create a price list."
        },
        "name": {
          "type": "string",
          "description": "The price list's name, such as 'Wholesale 15'. Required for a new price list."
        },
        "percentOff": {
          "type": ["number", "null"],
          "description": "The percentage off every product that has no price by SKU on the list, from 0 to 100. null or 0 means none."
        },
        "prices": {
          "type": "array",
          "description": "The prices by SKU, in the store's currency, replacing the list's current prices. A product's price on the list wins over the percentage off; the buyer pays the lower of it and the store's price. An empty list removes every price; leave it out to keep them.",
          "items": {
            "type": "object",
            "properties": {
              "sku": {
                "type": "string",
                "description": "The product's SKU, at most 100 characters."
              },
              "unitPrice": {
                "type": "number",
                "description": "The price of one unit, 0 or more."
              }
            },
            "required": ["sku", "unitPrice"],
            "additionalProperties": false
          }
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates a price list: name, percent off, and unit prices by SKU in the store's currency. On update the prices you send replace the stored ones; fields left out keep their values.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var store = services.GetRequiredService<IPriceListStore>();
        var priceListId = ReadString(arguments, "priceListId");
        PriceList list = null;

        if (priceListId is not null)
        {
            list = await store.FindByIdAsync(priceListId, cancellationToken);

            if (list is null)
            {
                return $"Nothing was saved: there is no price list with the id '{priceListId}'. Use listPriceLists to find it, or leave priceListId out to create one.";
            }

            if (!Has(arguments, "name") && !Has(arguments, "percentOff") && !Has(arguments, "prices"))
            {
                return "Nothing was changed: give the name, the percentage off or the prices to change.";
            }
        }

        var problems = new List<string>();
        var name = Has(arguments, "name") ? ReadString(arguments, "name") : list?.Name;
        string percentOff;

        if (Has(arguments, "percentOff"))
        {
            percentOff = null;

            if (ReadJson(arguments, "percentOff") is { } percentElement)
            {
                if (TryReadDecimal(percentElement, out var percent))
                {
                    percentOff = percent.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    problems.Add("percentOff must be a number from 0 to 100, or null for none.");
                }
            }
        }
        else
        {
            percentOff = list?.PercentOff?.ToString(CultureInfo.InvariantCulture);
        }

        var entries = Has(arguments, "prices") ? ReadPrices(arguments, problems) : [.. list?.Entries ?? []];

        foreach (var problem in PriceListRules.Check(name, percentOff, entries.Count, out _))
        {
            problems.Add(problem switch
            {
                PriceListProblem.NameRequired => "Name the price list.",
                PriceListProblem.PercentOffOutOfRange => "percentOff must be from 0 to 100, or null for none.",
                _ => $"A price list has at most {PriceListRules.MaxEntries} prices.",
            });
        }

        if (problems.Count > 0)
        {
            return $"Nothing was saved: {string.Join(" ", problems)}";
        }

        PriceListRules.Check(name, percentOff, entries.Count, out var percentValue);

        var storeCurrency = (await services.GetRequiredService<ISiteService>().GetSettingsAsync<CheckoutSettings>()).Currency;
        var previousCurrency = list?.Currency;
        var created = list is null;
        var now = services.GetRequiredService<IClock>().UtcNow;

        list ??= new PriceList { ItemId = IdGenerator.GenerateId(), CreatedUtc = now };

        PriceListRules.Apply(list, name, percentValue, entries, storeCurrency);

        if (created)
        {
            await store.CreateAsync(list, cancellationToken);
        }
        else
        {
            list.ModifiedUtc = now;
            await store.UpdateAsync(list, cancellationToken);
        }

        return Json(new
        {
            id = list.ItemId,
            name = list.Name,
            percentOff = list.PercentOff,
            currency = list.Currency,
            entryCount = list.Entries.Count,
            created,
            currencyChanged = !created && !string.IsNullOrEmpty(previousCurrency) && !string.Equals(previousCurrency, list.Currency, StringComparison.OrdinalIgnoreCase)
                ? $"The prices were in {previousCurrency} and are now read in the store's currency, {list.Currency}, as the editor saves them."
                : null,
            message = created
                ? "The price list was added. Choose it on the accounts that buy at it with saveB2BAccountSettings."
                : "The price list was saved.",
        });
    }

    private static List<PriceListEntry> ReadPrices(AIFunctionArguments arguments, List<string> problems)
    {
        var entries = new List<PriceListEntry>();

        if (ReadJson(arguments, "prices") is not { } element)
        {
            return entries;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            problems.Add("prices must be a list of objects with a sku and a unitPrice.");

            return entries;
        }

        var unread = new List<string>();

        foreach (var row in element.EnumerateArray())
        {
            var sku = row.ValueKind == JsonValueKind.Object && row.TryGetProperty("sku", out var skuElement) ? Text(skuElement) : null;
            var hasPrice = row.ValueKind == JsonValueKind.Object && row.TryGetProperty("unitPrice", out var priceElement) && TryReadDecimal(priceElement, out _);
            var unitPrice = hasPrice && TryReadDecimal(row.GetProperty("unitPrice"), out var value) ? value : -1m;
            var unknown = row.ValueKind == JsonValueKind.Object
                ? row.EnumerateObject().Select(property => property.Name).FirstOrDefault(field => field is not ("sku" or "unitPrice"))
                : null;

            if (unknown is not null || !hasPrice || !PriceListRules.TryAddEntry(entries, sku, unitPrice))
            {
                unread.Add(row.ToString());
            }
        }

        if (unread.Count > 0)
        {
            problems.Add($"These prices aren't a SKU of at most {PriceListRules.MaxSkuLength} characters and a unit price of 0 or more: {string.Join(" | ", unread.Take(5))}");
        }

        return entries;
    }
}
