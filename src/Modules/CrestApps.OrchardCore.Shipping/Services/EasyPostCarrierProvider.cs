using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.Shipping.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Shipping.Services;

/// <summary>
/// Quotes the carriers' rates and buys their labels through an EasyPost account.
/// </summary>
public sealed class EasyPostCarrierProvider : ICarrierProvider
{
    public const string TechnicalName = "EasyPost";
    public const string HttpClientName = "EasyPost";
    public const string ProtectionPurpose = "EasyPostSettings";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public EasyPostCarrierProvider(
        IHttpClientFactory httpClientFactory,
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<EasyPostCarrierProvider> logger,
        IStringLocalizer<EasyPostCarrierProvider> stringLocalizer)
    {
        _httpClientFactory = httpClientFactory;
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
        S = stringLocalizer;
    }

    public string Name => TechnicalName;

    public LocalizedString DisplayName => S["EasyPost"];

    public async Task<bool> IsEnabledAsync()
        => (await _siteService.GetSettingsAsync<EasyPostSettings>()).IsEnabled && !string.IsNullOrEmpty(await GetApiKeyAsync());

    /// <summary>
    /// Registers a webhook for the tracking updates at <paramref name="url"/>, signed with a secret made here. Returns
    /// EasyPost's id for it and the secret, or the error.
    /// </summary>
    public async Task<(string WebhookId, string Secret, string Error)> RegisterWebhookAsync(string apiKey, string url, CancellationToken cancellationToken = default)
    {
        var secret = Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var body = new JsonObject { ["webhook"] = new JsonObject { ["url"] = url, ["webhook_secret"] = secret } };
        var (response, error) = await SendAsync(HttpMethod.Post, "webhooks", body, cancellationToken, apiKey);

        return response?["id"]?.GetValue<string>() is { Length: > 0 } id ? (id, secret, null) : (null, null, error ?? S["EasyPost didn't register the webhook."].Value);
    }

    /// <summary>
    /// Removes a webhook registered earlier. A webhook EasyPost no longer has counts as removed.
    /// </summary>
    public async Task DeleteWebhookAsync(string apiKey, string webhookId, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(webhookId))
        {
            await SendAsync(HttpMethod.Delete, $"webhooks/{Uri.EscapeDataString(webhookId)}", body: null, cancellationToken, apiKey);
        }
    }

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
            },
        };

        if (!string.IsNullOrWhiteSpace(request.Reference))
        {
            body["shipment"]["reference"] = request.Reference;
        }

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
                Carrier = CarrierName(rate["carrier"]?.GetValue<string>()),
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
            Carrier = CarrierName(selected?["carrier"]?.GetValue<string>()),
            Service = selected?["service"]?.GetValue<string>(),
            Amount = ParseAmount(selected?["rate"]),
            Currency = selected?["currency"]?.GetValue<string>(),
            TrackingNumber = response["tracking_code"]?.GetValue<string>(),
            LabelUrl = response["postage_label"]?["label_url"]?.GetValue<string>(),
            TrackingUrl = response["tracker"]?["public_url"]?.GetValue<string>(),
        };
    }

    private async Task<(JsonObject Response, string Error)> SendAsync(HttpMethod method, string path, JsonObject body, CancellationToken cancellationToken, string apiKey = null)
    {
        apiKey ??= await GetApiKeyAsync();

        if (string.IsNullOrEmpty(apiKey))
        {
            return (null, S["EasyPost isn't set up. Enter its API key on the EasyPost tab of Settings › Commerce › Shipping."].Value);
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var message = new HttpRequestMessage(method, path)
        {
            // A buffered body, with its length: EasyPost reads a chunked one as empty.
            Content = body is null ? null : new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };

        message.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(apiKey + ":")));

        try
        {
            using var response = await client.SendAsync(message, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var node = string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json) as JsonObject;

            if (!response.IsSuccessStatusCode)
            {
                var error = DescribeError(node?["error"] as JsonObject) ?? S["EasyPost answered {0}.", (int)response.StatusCode].Value;

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
        => _dataProtectionProvider.UnprotectSettingOrDefault(ProtectionPurpose, value, _logger, "EasyPost key");

    // EasyPost names the carrier accounts it provides itself after the carrier, such as "FedExDefault" or "UPSDAP".
    internal static string CarrierName(string carrier)
        => carrier switch
        {
            null or "" => carrier,
            "UPSDAP" => "UPS",
            _ when carrier.Length > "Default".Length && carrier.EndsWith("Default", StringComparison.Ordinal) => carrier[..^"Default".Length],
            _ => carrier,
        };

    // EasyPost's error says what's wrong, and its field errors say where, such as "shipment.to_address.zip".
    internal static string DescribeError(JsonObject error)
    {
        var message = error?["message"]?.GetValue<string>();

        if (string.IsNullOrEmpty(message))
        {
            return null;
        }

        var fields = (error["errors"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(entry => (Field: entry["field"]?.GetValue<string>(), Message: entry["message"]?.GetValue<string>()))
            .Where(entry => !string.IsNullOrEmpty(entry.Field))
            .Select(entry => string.IsNullOrEmpty(entry.Message) ? entry.Field : $"{entry.Field}: {entry.Message}")
            .ToArray();

        return fields.Length == 0 ? message : $"{message} ({string.Join("; ", fields)})";
    }

    // Only the parts of the address that are there: EasyPost reads an empty value as one given.
    private static JsonObject ToEasyPost(Address address)
    {
        if (address is null)
        {
            return null;
        }

        var json = new JsonObject();

        void Add(string name, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                json[name] = value.Trim();
            }
        }

        Add("name", address.Name);
        Add("company", address.Company);
        Add("street1", address.AddressLine1);
        Add("street2", address.AddressLine2);
        Add("city", address.City);
        Add("state", address.Region);
        Add("zip", address.PostalCode);
        Add("country", address.Country);
        Add("phone", address.Phone);

        return json;
    }

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
