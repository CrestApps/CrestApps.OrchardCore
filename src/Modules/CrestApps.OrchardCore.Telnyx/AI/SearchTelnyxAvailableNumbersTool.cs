using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Telnyx.AI;

/// <summary>
/// Searches the numbers for sale, as the search form of the Telnyx numbers page does. Nothing is bought.
/// </summary>
internal sealed class SearchTelnyxAvailableNumbersTool : TelnyxToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "searchTelnyxAvailableNumbers";

    /// <summary>
    /// The most numbers a search returns, as on the page.
    /// </summary>
    public const int Limit = 30;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "countryCode": {
          "type": "string",
          "description": "The two-letter country code, for example US. US when not given."
        },
        "areaCode": {
          "type": "string",
          "description": "The area code (national destination code), for example 702."
        },
        "city": {
          "type": "string",
          "description": "The city."
        },
        "region": {
          "type": "string",
          "description": "The state or region, for example NV."
        },
        "contains": {
          "type": "string",
          "description": "Digits the number must contain."
        },
        "sms": {
          "type": "boolean",
          "description": "true to find only numbers that can also send and receive texts."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Searches phone numbers that can be bought, by country, area code, city, region, digits contained and whether SMS is needed. Returns up to 30 with their monthly and upfront cost. Nothing is bought.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var countryCode = AIModuleTools.ReadString(arguments, "countryCode");

        if (countryCode is not null && (countryCode.Length != 2 || !countryCode.All(char.IsAsciiLetter)))
        {
            return "Nothing was searched: countryCode must be a two-letter country code, for example US.";
        }

        var sms = arguments.TryGetValue("sms", out var value) && value is not null &&
            (value is bool flag ? flag : bool.TryParse(value.ToString(), out var parsed) && parsed);

        var (result, numbers) = await services.GetRequiredService<TelnyxApiClient>().SearchAvailableNumbersAsync(new TelnyxNumberSearch
        {
            CountryCode = countryCode ?? "US",
            AreaCode = AIModuleTools.ReadString(arguments, "areaCode"),
            City = AIModuleTools.ReadString(arguments, "city"),
            Region = AIModuleTools.ReadString(arguments, "region"),
            Contains = AIModuleTools.ReadString(arguments, "contains"),
            Features = sms ? ["voice", "sms"] : ["voice"],
            Limit = Limit,
        }, cancellationToken);

        if (!result.Succeeded)
        {
            return $"Telnyx did not search the numbers ({Describe(result)}).";
        }

        return AIModuleTools.Json(new
        {
            items = numbers.Select(number => new
            {
                phoneNumber = number.PhoneNumber,
                region = number.Region,
                monthlyCost = number.MonthlyCost,
                upfrontCost = number.UpfrontCost,
                currency = number.Currency,
                features = number.Features,
            }),
            total = numbers.Count,
            note = numbers.Count == 0
                ? "No number for sale matches. Try fewer filters."
                : "Buy a number on the Telnyx numbers page in the admin; it is billed to the Telnyx account.",
        });
    }
}
