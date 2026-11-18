using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.AIModels;

/// <summary>
/// Sets or clears what a deployment costs, as the Pricing card of the deployment editor does, with the same checks.
/// The prices only feed the cost estimates of the usage reports; nothing about how the site calls the model changes.
/// </summary>
public sealed class SetAIDeploymentPricingTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setAIDeploymentPricing";

    private const int MaxPrices = 30;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    $$"""
    {
      "type": "object",
      "properties": {
        "deploymentId": {
          "type": "string",
          "description": "The ID of the deployment, from listAIDeployments."
        },
        "currency": {
          "type": "string",
          "description": "The three-letter currency code of the prices, for example USD or EUR. Defaults to USD."
        },
        "prices": {
          "type": "array",
          "maxItems": 30,
          "description": "The full list of prices; it replaces the stored list. An empty list clears the pricing.",
          "items": {
            "type": "object",
            "properties": {
              "meter": {
                "type": "string",
                "enum": [{{string.Join(", ", AIUsageMeters.All.Select(meter => $"\"{meter.Name}\""))}}],
                "description": "What the price is for, for example InputTokens or OutputTokens."
              },
              "amount": {
                "type": "number",
                "minimum": 0,
                "description": "The price, in the currency, for 'per' units."
              },
              "per": {
                "type": "integer",
                "minimum": 1,
                "description": "How many units the amount is for, for example 1000000 for a price per million tokens. Defaults to the meter's usual quantity (a million for tokens)."
              },
              "countName": {
                "type": "string",
                "description": "For the AdditionalCount meter only: the name of the count the provider reports."
              },
              "aboveInputTokens": {
                "type": "integer",
                "minimum": 0,
                "description": "A long-context price: it applies only to requests with more input tokens than this."
              },
              "increment": {
                "type": "integer",
                "minimum": 0,
                "description": "The billing increment, for example 60 when audio is billed by the started minute."
              }
            },
            "required": ["meter", "amount"],
            "additionalProperties": false
          }
        }
      },
      "required": ["deploymentId", "prices"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Sets or clears what a deployment costs: currency and a list of prices per meter (for example input tokens per 1,000,000). Only affects cost estimates in the usage reports.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIDeployments))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("deploymentId", out var deploymentId) || string.IsNullOrWhiteSpace(deploymentId))
        {
            return "Nothing was changed: give the deploymentId, from listAIDeployments.";
        }

        var manager = services.GetRequiredService<INamedSourceCatalogManager<AIDeployment>>();
        var deployment = await manager.FindByIdAsync(deploymentId.Trim(), cancellationToken);

        if (deployment is null)
        {
            return $"Nothing was changed: no deployment has the ID '{deploymentId}'. Use listAIDeployments to find it.";
        }

        if (deployment.IsReadOnly)
        {
            return $"Nothing was changed: the deployment '{deployment.Name}' is defined in configuration (appsettings), so its prices are set there.";
        }

        if (!AgentToolArguments.TryReadJsonArray(arguments, "prices", out var priceArray) || priceArray is null)
        {
            return "Nothing was changed: give prices as a list; an empty list clears the pricing.";
        }

        if (priceArray.Count > MaxPrices)
        {
            return $"Nothing was changed: at most {MaxPrices} prices can be set.";
        }

        arguments.TryGetFirstString("currency", out var currency);
        currency = string.IsNullOrWhiteSpace(currency) ? null : currency.Trim().ToUpperInvariant();

        if (currency is not null && (currency.Length != 3 || !currency.All(char.IsAsciiLetter)))
        {
            return $"Nothing was changed: '{currency}' is not a three-letter currency code such as USD.";
        }

        var prices = new List<AIDeploymentPrice>();

        for (var index = 0; index < priceArray.Count; index++)
        {
            if (!TryReadPrice(priceArray[index], out var price, out var error))
            {
                return $"Nothing was changed: price {index + 1}: {error}";
            }

            prices.Add(price);
        }

        deployment.SetPricing(currency, prices);
        await manager.UpdateAsync(deployment, cancellationToken: cancellationToken);

        var pricing = deployment.GetPricing();
        var kept = pricing?.Prices ?? [];

        return AgentToolResults.Json(new
        {
            id = deployment.ItemId,
            name = deployment.Name,
            cleared = pricing is null,
            currency = pricing?.Currency,
            prices = kept.Select(price => new
            {
                meter = price.Meter,
                amount = price.Amount,
                per = price.Per,
                countName = price.CountName,
                aboveInputTokens = price.AboveInputTokens,
                increment = price.Increment > 0 ? price.Increment : (long?)null,
            }).ToList(),
            ignoredFreePrices = prices.Count - kept.Count,
            note = "Only the cost estimates of the usage reports change.",
        });
    }

    private static bool TryReadPrice(JsonNode node, out AIDeploymentPrice price, out string error)
    {
        price = null;

        if (node is not JsonObject item)
        {
            error = "each price must be an object with meter and amount.";

            return false;
        }

        var meter = AIUsageMeters.Find(ReadText(item["meter"]));

        // The same checks, in the same words, as the Pricing card of the deployment editor.
        if (meter is null)
        {
            error = $"Select what each price is for: one of {string.Join(", ", AIUsageMeters.All.Select(entry => entry.Name))}.";

            return false;
        }

        var countName = ReadText(item["countName"]);

        if (meter.RequiresCountName && string.IsNullOrWhiteSpace(countName))
        {
            error = "Enter the name of the count an 'Other reported count' price is for (countName).";

            return false;
        }

        if (!TryReadNumber(item["amount"], out var amount))
        {
            error = "amount must be a number.";

            return false;
        }

        if (amount < 0)
        {
            error = "A price cannot be negative.";

            return false;
        }

        long per = meter.Quantities.Count > 0 ? meter.Quantities[0].Per : 1;

        if (item["per"] is not null)
        {
            if (!TryReadNumber(item["per"], out var perValue) || perValue < 1 || perValue != Math.Floor(perValue))
            {
                error = "per must be a whole number of at least 1.";

                return false;
            }

            per = (long)perValue;
        }

        long? aboveInputTokens = null;
        long increment = 0;

        if (item["aboveInputTokens"] is not null)
        {
            if (!TryReadNumber(item["aboveInputTokens"], out var above) || above < 0)
            {
                error = "The long-context threshold and billing increment cannot be negative.";

                return false;
            }

            aboveInputTokens = (long)above;
        }

        if (item["increment"] is not null)
        {
            if (!TryReadNumber(item["increment"], out var step) || step < 0)
            {
                error = "The long-context threshold and billing increment cannot be negative.";

                return false;
            }

            increment = (long)step;
        }

        price = new AIDeploymentPrice
        {
            Meter = meter.Name,
            Amount = amount,
            Per = per,
            CountName = meter.RequiresCountName ? countName.Trim() : null,
            AboveInputTokens = aboveInputTokens,
            Increment = increment,
        };

        error = null;

        return true;
    }

    private static string ReadText(JsonNode node)
        => node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

    private static bool TryReadNumber(JsonNode node, out decimal number)
    {
        number = 0;

        if (node is not JsonValue value)
        {
            return false;
        }

        return value.GetValueKind() switch
        {
            JsonValueKind.Number => decimal.TryParse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number),
            JsonValueKind.String => decimal.TryParse(value.GetValue<string>(), NumberStyles.Float, CultureInfo.InvariantCulture, out number),
            _ => false,
        };
    }
}
