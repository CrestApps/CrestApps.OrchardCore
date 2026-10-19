using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Shipping.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Shipping.Services;

/// <summary>
/// Quotes the carriers' rates and buys their labels through an EasyPost account.
/// </summary>
public sealed class EasyPostCarrierService : ICarrierService
{
    public const string HttpClientName = "EasyPost";
    public const string ProtectionPurpose = "EasyPostSettings";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public EasyPostCarrierService(
        IHttpClientFactory httpClientFactory,
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<EasyPostCarrierService> logger,
        IStringLocalizer<EasyPostCarrierService> stringLocalizer)
    {
        _httpClientFactory = httpClientFactory;
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
        S = stringLocalizer;
    }

    public string Name => "EasyPost";

    public LocalizedString DisplayName => S["EasyPost"];

    public async Task<bool> IsConfiguredAsync()
        => !string.IsNullOrEmpty(await GetApiKeyAsync());

    public async Task<CarrierQuote> QuoteAsync(CarrierShipmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parcel = new JsonObject { ["weight"] = request.Parcel.WeightOunces };

        if (request.Parcel.LengthInches is { } length && request.Parcel.WidthInches is { } width && request.Parcel.HeightInches is { } height)
        {
            parcel["length"] = length;
            parcel["width"] = width;
            parcel["height"] = height;
        }

        var body = new JsonObject
        {
            ["shipment"] = new JsonObject
            {
                ["to_address"] = ToEasyPost(request.To),
                ["from_address"] = ToEasyPost(request.From),
                ["parcel"] = parcel,
                ["reference"] = request.Reference,
            },
        };

        var (response, error) = await SendAsync(HttpMethod.Post, "shipments", body, cancellationToken);

        if (response is null)
        {
            return new CarrierQuote { Error = error };
        }

        var rates = (response["rates"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(rate => new CarrierRate
            {
                RateId = rate["id"]?.GetValue<string>(),
                Carrier = rate["carrier"]?.GetValue<string>(),
                Service = rate["service"]?.GetValue<string>(),
                Amount = ParseAmount(rate["rate"]),
                Currency = rate["currency"]?.GetValue<string>(),
                DeliveryDays = rate["delivery_days"] is JsonValue days && days.TryGetValue<int>(out var value) ? value : null,
            })
            .Where(rate => !string.IsNullOrEmpty(rate.RateId))
            .ToArray();

        // No rate at all: the carriers' messages say why, such as an address they can't deliver to.
        if (rates.Length == 0)
        {
            var messages = (response["messages"] as JsonArray ?? []).OfType<JsonObject>().Select(message => message["message"]?.GetValue<string>()).Where(message => !string.IsNullOrEmpty(message));

            return new CarrierQuote { Error = string.Join(" ", messages).Trim() is { Length: > 0 } text ? text : S["No carrier quoted this parcel."].Value };
        }

        return new CarrierQuote { QuoteId = response["id"]?.GetValue<string>(), Rates = rates };
    }

    public async Task<CarrierLabel> BuyLabelAsync(string quoteId, string rateId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(quoteId) || string.IsNullOrEmpty(rateId))
        {
            return new CarrierLabel { Error = S["Choose a rate to buy."].Value };
        }

        var body = new JsonObject { ["rate"] = new JsonObject { ["id"] = rateId } };
        var (response, error) = await SendAsync(HttpMethod.Post, $"shipments/{Uri.EscapeDataString(quoteId)}/buy", body, cancellationToken);

        if (response is null)
        {
            return new CarrierLabel { Error = error };
        }

        var selected = response["selected_rate"] as JsonObject;

        return new CarrierLabel
        {
            Carrier = selected?["carrier"]?.GetValue<string>(),
            Service = selected?["service"]?.GetValue<string>(),
            Amount = ParseAmount(selected?["rate"]),
            Currency = selected?["currency"]?.GetValue<string>(),
            TrackingNumber = response["tracking_code"]?.GetValue<string>(),
            LabelUrl = response["postage_label"]?["label_url"]?.GetValue<string>(),
            TrackingUrl = response["tracker"]?["public_url"]?.GetValue<string>(),
        };
    }

    private async Task<(JsonObject Response, string Error)> SendAsync(HttpMethod method, string path, JsonObject body, CancellationToken cancellationToken)
    {
        var apiKey = await GetApiKeyAsync();

        if (string.IsNullOrEmpty(apiKey))
        {
            return (null, S["EasyPost isn't set up. Enter its API key in Settings › Commerce › Shipping."].Value);
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var message = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body),
        };

        message.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(apiKey + ":")));

        try
        {
            using var response = await client.SendAsync(message, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var node = string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json) as JsonObject;

            if (!response.IsSuccessStatusCode)
            {
                var error = node?["error"]?["message"]?.GetValue<string>() ?? S["EasyPost answered {0}.", (int)response.StatusCode].Value;

                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("EasyPost answered {StatusCode} to {Path}: {Error}", (int)response.StatusCode, path, error);
                }

                return (null, error);
            }

            return (node, null);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(exception, "EasyPost couldn't be reached for {Path}.", path);

            return (null, S["EasyPost couldn't be reached. Try again in a moment."].Value);
        }
    }

    private async Task<string> GetApiKeyAsync()
    {
        var settings = await _siteService.GetSettingsAsync<EasyPostSettings>();

        return Unprotect(settings.ApiKey);
    }

    internal string Unprotect(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            return _dataProtectionProvider.CreateProtector(ProtectionPurpose).Unprotect(value);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            _logger.LogWarning("The EasyPost settings couldn't be read; enter the API key again.");

            return null;
        }
    }

    private static JsonObject ToEasyPost(Address address)
        => address is null
            ? null
            : new JsonObject
            {
                ["name"] = address.Name,
                ["company"] = address.Company,
                ["street1"] = address.AddressLine1,
                ["street2"] = address.AddressLine2,
                ["city"] = address.City,
                ["state"] = address.Region,
                ["zip"] = address.PostalCode,
                ["country"] = address.Country,
                ["phone"] = address.Phone,
            };

    // EasyPost sends amounts as strings, such as "5.93".
    private static decimal ParseAmount(JsonNode node)
    {
        if (node is not JsonValue value)
        {
            return 0m;
        }

        if (value.TryGetValue<string>(out var text))
        {
            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
        }

        return value.TryGetValue<decimal>(out var number) ? number : 0m;
    }
}
