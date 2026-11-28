using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Checkout.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Accounts.B2B.AI;

/// <summary>
/// Changes an account's business buying settings with the Business buying card's rules
/// (<see cref="B2BAccountSettingsRules"/>): net days from 0 to 365 or none, a deposit from 0 to 100 percent, no negative
/// credit limit or budget, and one order limit above zero for each buyer. Only the settings given change; punchout is
/// never changed here. Stricter than the card: every buyer must be a member of the account, and the price list must
/// exist. It needs <c>ManageB2BAccounts</c> and <c>EditContent</c> on the account.
/// </summary>
internal sealed class SaveB2BAccountSettingsTool : B2BAccountsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveB2BAccountSettings";

    private static readonly string[] _settingNames =
    [
        "enabled", "taxId", "billingEmail", "netDays", "depositPercent", "creditLimit", "requirePurchaseOrder", "priceListId", "monthlyBudget", "orderLimits",
    ];

    private static readonly JsonElement _jsonSchema = Schema(
    $$"""
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The account's id, from listAccounts."
        },
        "enabled": {
          "type": "boolean",
          "description": "Whether the account buys as a business. While it is off, its buyers shop as themselves."
        },
        "taxId": {
          "type": "string",
          "description": "The account's tax or VAT number, printed on its invoices. An empty text removes it."
        },
        "billingEmail": {
          "type": "string",
          "description": "Where the account's invoices are sent, besides to the buyer. An empty text removes it."
        },
        "netDays": {
          "type": ["integer", "null"],
          "description": "The days an invoice for what the account buys on its terms is due in, from 0 to 365, such as 30 for net-30. null removes the terms, so the account pays at checkout."
        },
        "depositPercent": {
          "type": "number",
          "description": "The part of an order, from 0 to 100 percent, paid at checkout before the rest goes on the account."
        },
        "creditLimit": {
          "type": ["number", "null"],
          "description": "The most the account may owe at once, in the store's currency. null removes the limit."
        },
        "requirePurchaseOrder": {
          "type": "boolean",
          "description": "Whether its buyers must give a purchase order number at checkout."
        },
        "priceListId": {
          "type": "string",
          "description": "The id of the price list its buyers pay, from listPriceLists. An empty text puts the account back on the store's prices."
        },
        "monthlyBudget": {
          "type": ["number", "null"],
          "description": "How much its buyers may order in a calendar month, in the store's currency; orders past it need an account admin's approval. null removes the budget."
        },
        "orderLimits": {
          "type": "array",
          "description": "The order limits of the account's buyers, replacing the current ones: an order above a buyer's limit waits for an account admin's approval. An empty list removes every limit; leave it out to keep them.",
          "items": {
            "type": "object",
            "properties": {
              "user": {
                "type": "string",
                "description": "The buyer: the user name or email of one of the account's members."
              },
              "limit": {
                "type": ["number", "null"],
                "description": "The most one of the buyer's orders may come to, in the store's currency. null or 0 means no limit."
              }
            },
            "required": ["user"],
            "additionalProperties": false
          }
        },
        "saveAsDraft": {
          "type": "boolean",
          "description": "{{SaveAsDraftDescription}}"
        }
      },
      "required": ["contentItemId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Turns business buying on or off for an account and sets tax id, billing email, payment terms (net days, deposit percent, credit limit, purchase order required), price list, monthly budget and per-buyer order limits. Buyers must already be members of the account. Only the settings given change.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (account, error) = await LoadAccountAsync(services, ReadString(arguments, "contentItemId"));

        if (account is null)
        {
            return error;
        }

        if (!await IsAuthorizedAsync(services, caller, CommonPermissions.EditContent, account))
        {
            return NotAllowedMessage;
        }

        if (!_settingNames.Any(name => Has(arguments, name)))
        {
            return "Nothing was changed: give at least one setting to change.";
        }

        var settings = B2BAccountSettings.From(account.TryGet<B2BAccountPart>(out var stored) ? stored : null);
        var problems = new List<string>();

        if (Has(arguments, "enabled"))
        {
            settings.Enabled = ReadBool(arguments, "enabled") ?? settings.Enabled;
        }

        if (Has(arguments, "taxId"))
        {
            settings.TaxId = ReadString(arguments, "taxId");
        }

        if (Has(arguments, "billingEmail"))
        {
            settings.BillingEmail = ReadString(arguments, "billingEmail");
        }

        if (Has(arguments, "netDays"))
        {
            settings.NetDays = ReadNullableNumber(arguments, "netDays", problems, wholeNumber: true) is { } days ? (int)days : null;
        }

        if (Has(arguments, "depositPercent"))
        {
            settings.DepositPercent = ReadNullableNumber(arguments, "depositPercent", problems) ?? 0m;
        }

        if (Has(arguments, "creditLimit"))
        {
            settings.CreditLimit = ReadNullableNumber(arguments, "creditLimit", problems);
        }

        if (Has(arguments, "monthlyBudget"))
        {
            settings.MonthlyBudget = ReadNullableNumber(arguments, "monthlyBudget", problems);
        }

        if (Has(arguments, "requirePurchaseOrder"))
        {
            settings.RequirePurchaseOrder = ReadBool(arguments, "requirePurchaseOrder") ?? settings.RequirePurchaseOrder;
        }

        string priceListName = null;

        if (Has(arguments, "priceListId"))
        {
            settings.PriceListId = ReadString(arguments, "priceListId");

            if (settings.PriceListId is not null)
            {
                var list = await services.GetRequiredService<IPriceListStore>().FindByIdAsync(settings.PriceListId, cancellationToken);

                if (list is null)
                {
                    problems.Add($"There is no price list with the id '{settings.PriceListId}'. Use listPriceLists to find it.");
                }
                else
                {
                    priceListName = list.Name;
                }
            }
        }

        if (Has(arguments, "orderLimits"))
        {
            settings.OrderLimits = ReadOrderLimits(arguments, GetMembers(account), problems);
        }

        foreach (var problem in B2BAccountSettingsRules.CheckTerms(settings.NetDays, settings.DepositPercent, settings.CreditLimit, settings.MonthlyBudget))
        {
            problems.Add(problem switch
            {
                B2BAccountSettingsProblem.NetDaysOutOfRange => "Net days must be from 0 to 365, or null for no terms.",
                B2BAccountSettingsProblem.DepositOutOfRange => "The deposit must be from 0 to 100 percent.",
                _ => "A credit limit or budget can't be negative.",
            });
        }

        if (problems.Count > 0)
        {
            return $"Nothing was changed: {string.Join(" ", problems)}";
        }

        var (item, published, saveError) = await SaveAccountAsync(services, caller, account, ReadBool(arguments, "saveAsDraft") == true, version =>
            version.Alter<B2BAccountPart>(part => B2BAccountSettingsRules.ApplySettings(part, settings)));

        if (item is null)
        {
            return saveError;
        }

        var members = GetMembers(item);
        var storeCurrency = (await services.GetRequiredService<ISiteService>().GetSettingsAsync<CheckoutSettings>()).Currency;

        return Json(new
        {
            contentItemId = item.ContentItemId,
            account = item.DisplayText,
            enabled = settings.Enabled,
            taxId = settings.TaxId,
            billingEmail = settings.BillingEmail,
            currency = storeCurrency,
            terms = new
            {
                netDays = settings.NetDays,
                depositPercent = settings.DepositPercent,
                creditLimit = settings.CreditLimit,
                requirePurchaseOrder = settings.RequirePurchaseOrder,
            },
            monthlyBudget = settings.MonthlyBudget,
            priceList = settings.PriceListId is null ? null : new { id = settings.PriceListId, name = priceListName },
            orderLimits = settings.OrderLimits.Select(limit => new
            {
                user = members.FindMember(limit.UserId) is { } member ? member.Email ?? member.UserName : limit.UserId,
                limit = limit.Limit,
            }).ToList(),
            published,
            savedAsDraft = !published,
            message = published
                ? "The business buying settings were saved and the account was published."
                : "The business buying settings were saved in a draft. The published account, if any, keeps its current settings until the draft is published.",
        });
    }

    private static List<BuyerOrderLimit> ReadOrderLimits(AIFunctionArguments arguments, AccountMembersPart members, List<string> problems)
    {
        if (ReadJson(arguments, "orderLimits") is not { } element)
        {
            return [];
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            problems.Add("orderLimits must be a list of objects with a user and a limit.");

            return [];
        }

        var rows = new List<(string UserId, decimal? Limit)>();

        foreach (var row in element.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty("user", out var userElement) || Text(userElement) is not { } user)
            {
                problems.Add("Each order limit needs the user name or email of one of the account's members.");

                continue;
            }

            var unknown = row.EnumerateObject().Select(property => property.Name).FirstOrDefault(field => field is not ("user" or "limit"));

            if (unknown is not null)
            {
                problems.Add($"'{unknown}' is not a field of an order limit. Use user and limit.");

                continue;
            }

            if (FindMember(members, user) is not { } member)
            {
                problems.Add($"{user} is not a member of the account. Add them with setAccountMembers first.");

                continue;
            }

            decimal? limit = null;

            if (row.TryGetProperty("limit", out var limitElement) && limitElement.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
            {
                if (!TryReadDecimal(limitElement, out var value) || value < 0m)
                {
                    problems.Add($"The order limit of {user} must be a number of 0 or more.");

                    continue;
                }

                limit = value;
            }

            rows.Add((member.UserId, limit));
        }

        return B2BAccountSettingsRules.CleanOrderLimits(rows);
    }

    private static decimal? ReadNullableNumber(AIFunctionArguments arguments, string name, List<string> problems, bool wholeNumber = false)
    {
        if (ReadJson(arguments, name) is not { } element)
        {
            return null;
        }

        if (!TryReadDecimal(element, out var value) || (wholeNumber && decimal.Truncate(value) != value) || (wholeNumber && value is > int.MaxValue or < int.MinValue))
        {
            problems.Add(wholeNumber ? $"{name} must be a whole number." : $"{name} must be a number.");

            return null;
        }

        return value;
    }
}
