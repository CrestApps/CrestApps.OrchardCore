using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Addresses.Core.Autocomplete;
using CrestApps.OrchardCore.Addresses.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Addresses.GooglePlaces;

/// <summary>
/// Address suggestions from Google Places (the Places API, New): autocomplete for what was typed, and the address
/// components of the place picked. The key is the store's, kept protected in the site settings and sent only from the
/// server.
/// </summary>
public sealed class GooglePlacesAddressAutocompleteProvider : IAddressAutocompleteProvider
{
    public const string HttpClientName = "GooglePlaces";

    // Countries that write the house number before the street.
    private static readonly HashSet<string> _numberFirst = new(StringComparer.OrdinalIgnoreCase)
    {
        "US", "CA", "GB", "IE", "AU", "NZ", "FR", "ZA", "IN", "SG", "PH", "MY", "LU", "IL",
    };

    private readonly ISiteService _siteService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDataProtector _protector;
    private readonly ILogger _logger;

    private string _apiKey;
    private bool _keyRead;

    public GooglePlacesAddressAutocompleteProvider(
        ISiteService siteService,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<GooglePlacesAddressAutocompleteProvider> logger)
    {
        _siteService = siteService;
        _httpClientFactory = httpClientFactory;
        _protector = dataProtectionProvider.CreateProtector(GooglePlacesSettings.ProtectionPurpose);
        _logger = logger;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => !string.IsNullOrEmpty(await GetApiKeyAsync());

    public async Task<IReadOnlyList<AddressSuggestion>> SuggestAsync(AddressSuggestionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await GetApiKeyAsync() is not { Length: > 0 } key)
        {
            return [];
        }

        var body = new AutocompleteRequest
        {
            Input = request.Text,
            IncludedRegionCodes = string.IsNullOrEmpty(request.Country) ? null : [request.Country.ToLowerInvariant()],
            LanguageCode = request.Language,
            SessionToken = request.SessionToken,
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "https://places.googleapis.com/v1/places:autocomplete")
        {
            Content = JsonContent.Create(body, options: _json),
        };
        message.Headers.Add("X-Goog-Api-Key", key);

        try
        {
            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(message, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google Places autocomplete answered {StatusCode}.", (int)response.StatusCode);

                return [];
            }

            var answer = await response.Content.ReadFromJsonAsync<AutocompleteResponse>(_json, cancellationToken);

            return [.. (answer?.Suggestions ?? [])
                .Select(suggestion => suggestion.PlacePrediction)
                .Where(prediction => !string.IsNullOrEmpty(prediction?.PlaceId) && !string.IsNullOrEmpty(prediction.Text?.Text))
                .Select(prediction => new AddressSuggestion(prediction.PlaceId, prediction.Text.Text))];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(exception, "Google Places autocomplete couldn't be reached.");

            return [];
        }
    }

    public async Task<Address> ResolveAsync(string suggestionId, string sessionToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(suggestionId) || await GetApiKeyAsync() is not { Length: > 0 } key)
        {
            return null;
        }

        var url = $"https://places.googleapis.com/v1/places/{Uri.EscapeDataString(suggestionId)}" +
            (string.IsNullOrEmpty(sessionToken) ? string.Empty : $"?sessionToken={Uri.EscapeDataString(sessionToken)}");

        using var message = new HttpRequestMessage(HttpMethod.Get, url);
        message.Headers.Add("X-Goog-Api-Key", key);
        message.Headers.Add("X-Goog-FieldMask", "addressComponents");

        try
        {
            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(message, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google Places details answered {StatusCode}.", (int)response.StatusCode);

                return null;
            }

            var place = await response.Content.ReadFromJsonAsync<PlaceResponse>(_json, cancellationToken);

            return ToAddress(place?.AddressComponents ?? []);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(exception, "Google Places details couldn't be reached.");

            return null;
        }
    }

    /// <summary>
    /// Returns an address from a place's address components: the street and number in the country's order, the town,
    /// the region's code, the postal code and the country.
    /// </summary>
    internal static Address ToAddress(IReadOnlyList<AddressComponent> components)
    {
        string Long(string type) => components.FirstOrDefault(component => component.Types?.Contains(type) == true)?.LongText;
        string Short(string type) => components.FirstOrDefault(component => component.Types?.Contains(type) == true)?.ShortText;

        var country = Short("country")?.ToUpperInvariant();
        var number = Long("street_number");
        var street = Long("route");
        var line1 = string.IsNullOrEmpty(number)
            ? street
            : string.IsNullOrEmpty(street)
                ? number
                : _numberFirst.Contains(country ?? string.Empty) ? $"{number} {street}" : $"{street} {number}";

        return new Address
        {
            AddressLine1 = line1 ?? Long("premise"),
            AddressLine2 = Long("subpremise"),
            DependentLocality = Long("sublocality") ?? Long("sublocality_level_1"),
            City = Long("locality") ?? Long("postal_town") ?? Long("administrative_area_level_2"),
            Region = Short("administrative_area_level_1"),
            PostalCode = Long("postal_code"),
            Country = country,
        };
    }

    private async Task<string> GetApiKeyAsync()
    {
        if (_keyRead)
        {
            return _apiKey;
        }

        _keyRead = true;

        var settings = await _siteService.GetSettingsAsync<GooglePlacesSettings>();

        if (string.IsNullOrEmpty(settings.ApiKey))
        {
            return null;
        }

        try
        {
            _apiKey = _protector.Unprotect(settings.ApiKey);
        }
        catch (System.Security.Cryptography.CryptographicException exception)
        {
            _logger.LogWarning(exception, "The Google Places API key couldn't be read; enter it again in the settings.");
        }

        return _apiKey;
    }

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed class AutocompleteRequest
    {
        public string Input { get; set; }

        public string[] IncludedRegionCodes { get; set; }

        public string LanguageCode { get; set; }

        public string SessionToken { get; set; }
    }

    private sealed class AutocompleteResponse
    {
        public List<Suggestion> Suggestions { get; set; }
    }

    private sealed class Suggestion
    {
        public PlacePrediction PlacePrediction { get; set; }
    }

    private sealed class PlacePrediction
    {
        public string PlaceId { get; set; }

        public FormattedText Text { get; set; }
    }

    private sealed class FormattedText
    {
        public string Text { get; set; }
    }

    private sealed class PlaceResponse
    {
        public List<AddressComponent> AddressComponents { get; set; }
    }

    internal sealed class AddressComponent
    {
        public string LongText { get; set; }

        public string ShortText { get; set; }

        public List<string> Types { get; set; }
    }
}
