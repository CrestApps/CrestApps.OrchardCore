using System.Globalization;
using System.Text.Json;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Phone numbers: searching the numbers for sale, buying one, listing and releasing the account's numbers, and the
/// caller name a number shows.
/// </summary>
public sealed partial class TelnyxApiClient
{
    /// <summary>
    /// Searches the numbers for sale.
    /// </summary>
    /// <param name="search">What to look for.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The numbers, or the failure.</returns>
    public async Task<(TelnyxApiResult Result, IReadOnlyList<TelnyxAvailableNumber> Numbers)> SearchAvailableNumbersAsync(TelnyxNumberSearch search, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);

        var query = new List<string>
        {
            $"filter[country_code]={Uri.EscapeDataString(string.IsNullOrWhiteSpace(search.CountryCode) ? "US" : search.CountryCode.Trim().ToUpperInvariant())}",
            $"filter[limit]={Math.Clamp(search.Limit, 1, 100).ToString(CultureInfo.InvariantCulture)}",
        };

        AddFilter(query, "national_destination_code", search.AreaCode);
        AddFilter(query, "locality", search.City);
        AddFilter(query, "administrative_area", search.Region);
        AddFilter(query, "phone_number[contains]", search.Contains);

        foreach (var feature in search.Features ?? [])
        {
            AddFilter(query, "features][", feature);
        }

        var (result, json) = await SendAsync(HttpMethod.Get, "available_phone_numbers?" + string.Join('&', query), body: null, retryable: true, cancellationToken);

        return (result, ReadArray(json).Select(ReadAvailableNumber).Where(number => number is not null).ToList());
    }

    /// <summary>
    /// Buys numbers and assigns them to the voice connection and the messaging profile.
    /// </summary>
    /// <param name="phoneNumbers">The numbers, in E.164 form.</param>
    /// <param name="connectionId">The voice connection, or <see langword="null"/> for the configured one.</param>
    /// <param name="messagingProfileId">The messaging profile, when texts should work too.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The order's id and status, or the failure.</returns>
    public async Task<(TelnyxApiResult Result, string OrderId, string Status)> OrderNumbersAsync(
        IEnumerable<string> phoneNumbers,
        string connectionId = null,
        string messagingProfileId = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["phone_numbers"] = (phoneNumbers ?? []).Where(number => !string.IsNullOrWhiteSpace(number)).Select(number => new Dictionary<string, string> { ["phone_number"] = number.Trim() }).ToList(),
        };

        var connection = string.IsNullOrWhiteSpace(connectionId) ? CurrentOptions.ConnectionId : connectionId;

        if (!string.IsNullOrWhiteSpace(connection))
        {
            body["connection_id"] = connection;
        }

        if (!string.IsNullOrWhiteSpace(messagingProfileId))
        {
            body["messaging_profile_id"] = messagingProfileId;
        }

        // Never retried: a repeated order buys the numbers twice when the first one went through.
        var (result, json) = await SendAsync(HttpMethod.Post, "number_orders", body, retryable: false, cancellationToken);

        return (result, ReadDataString(json, "id"), ReadDataString(json, "status"));
    }

    /// <summary>
    /// Lists the account's numbers.
    /// </summary>
    /// <param name="page">The page, from 1.</param>
    /// <param name="pageSize">The page size, at most 250.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The numbers, or the failure.</returns>
    public async Task<(TelnyxApiResult Result, IReadOnlyList<TelnyxOwnedNumber> Numbers)> ListOwnedNumbersAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var path = $"phone_numbers?page[number]={Math.Max(1, page).ToString(CultureInfo.InvariantCulture)}&page[size]={Math.Clamp(pageSize, 1, 250).ToString(CultureInfo.InvariantCulture)}";
        var (result, json) = await SendAsync(HttpMethod.Get, path, body: null, retryable: true, cancellationToken);

        return (result, ReadArray(json).Select(item => new TelnyxOwnedNumber
        {
            Id = ReadString(item, "id"),
            PhoneNumber = ReadString(item, "phone_number"),
            Status = ReadString(item, "status"),
            ConnectionId = ReadString(item, "connection_id"),
            MessagingProfileId = ReadString(item, "messaging_profile_id"),
        }).Where(number => !string.IsNullOrEmpty(number.Id)).ToList());
    }

    /// <summary>
    /// Releases one of the account's numbers. It stops working at once and can be bought by anyone.
    /// </summary>
    /// <param name="phoneNumberId">The number's Telnyx id.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    public async Task<TelnyxApiResult> ReleaseNumberAsync(string phoneNumberId, CancellationToken cancellationToken = default)
    {
        var (result, _) = await SendAsync(HttpMethod.Delete, $"phone_numbers/{Uri.EscapeDataString(phoneNumberId ?? string.Empty)}", body: null, retryable: false, cancellationToken);

        return result;
    }

    /// <summary>
    /// Sets the caller name (CNAM) a number shows on the phones it calls.
    /// </summary>
    /// <param name="phoneNumberId">The number's Telnyx id.</param>
    /// <param name="callerName">The name, at most 15 letters, digits and spaces; empty stops listing one.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    public async Task<TelnyxApiResult> SetCallerNameAsync(string phoneNumberId, string callerName, CancellationToken cancellationToken = default)
    {
        var name = NormalizeCallerName(callerName);
        var body = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["cnam_listing"] = new Dictionary<string, object>
            {
                ["cnam_listing_enabled"] = name.Length > 0,
                ["cnam_listing_details"] = name,
            },
        };

        var (result, _) = await SendAsync(HttpMethod.Patch, $"phone_numbers/{Uri.EscapeDataString(phoneNumberId ?? string.Empty)}/voice", body, retryable: true, cancellationToken);

        return result;
    }

    /// <summary>
    /// Keeps the letters, digits and spaces of a caller name, at most 15, as carriers allow.
    /// </summary>
    /// <param name="callerName">The name.</param>
    /// <returns>The name carriers take.</returns>
    public static string NormalizeCallerName(string callerName)
    {
        var kept = new string((callerName ?? string.Empty).Where(c => char.IsAsciiLetterOrDigit(c) || c == ' ').ToArray()).Trim();

        return kept.Length > 15 ? kept[..15].TrimEnd() : kept;
    }

    private static void AddFilter(List<string> query, string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add($"filter[{name}]={Uri.EscapeDataString(value.Trim())}");
        }
    }

    private static IEnumerable<JsonElement> ReadArray(JsonElement? json)
        => json is { ValueKind: JsonValueKind.Object } root && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
            ? data.EnumerateArray().ToList()
            : [];

    private static string ReadString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static TelnyxAvailableNumber ReadAvailableNumber(JsonElement item)
    {
        var number = ReadString(item, "phone_number");

        if (string.IsNullOrEmpty(number))
        {
            return null;
        }

        var region = item.TryGetProperty("region_information", out var regions) && regions.ValueKind == JsonValueKind.Array
            ? string.Join(", ", regions.EnumerateArray().Select(entry => ReadString(entry, "region_name")).Where(name => !string.IsNullOrEmpty(name)))
            : null;
        var cost = item.TryGetProperty("cost_information", out var costs) ? costs : default;

        return new TelnyxAvailableNumber
        {
            PhoneNumber = number,
            Region = region,
            MonthlyCost = ReadString(cost, "monthly_cost"),
            UpfrontCost = ReadString(cost, "upfront_cost"),
            Currency = ReadString(cost, "currency"),
            Features = item.TryGetProperty("features", out var features) && features.ValueKind == JsonValueKind.Array
                ? features.EnumerateArray().Select(feature => ReadString(feature, "name")).Where(name => !string.IsNullOrEmpty(name)).ToList()
                : [],
        };
    }
}

/// <summary>
/// What to look for among the numbers for sale.
/// </summary>
public sealed class TelnyxNumberSearch
{
    /// <summary>
    /// Gets or sets the two-letter country code. Defaults to US.
    /// </summary>
    public string CountryCode { get; set; } = "US";

    /// <summary>
    /// Gets or sets the area code.
    /// </summary>
    public string AreaCode { get; set; }

    /// <summary>
    /// Gets or sets the city.
    /// </summary>
    public string City { get; set; }

    /// <summary>
    /// Gets or sets the state or province, such as NV.
    /// </summary>
    public string Region { get; set; }

    /// <summary>
    /// Gets or sets digits the number contains.
    /// </summary>
    public string Contains { get; set; }

    /// <summary>
    /// Gets or sets the features the number must have, such as <c>voice</c> and <c>sms</c>.
    /// </summary>
    public IList<string> Features { get; set; } = [];

    /// <summary>
    /// Gets or sets how many to list, at most 100.
    /// </summary>
    public int Limit { get; set; } = 20;
}

/// <summary>
/// A number for sale.
/// </summary>
public sealed class TelnyxAvailableNumber
{
    /// <summary>Gets or sets the number.</summary>
    public string PhoneNumber { get; set; }

    /// <summary>Gets or sets where it is.</summary>
    public string Region { get; set; }

    /// <summary>Gets or sets what it costs a month.</summary>
    public string MonthlyCost { get; set; }

    /// <summary>Gets or sets what it costs to buy.</summary>
    public string UpfrontCost { get; set; }

    /// <summary>Gets or sets the currency of the costs.</summary>
    public string Currency { get; set; }

    /// <summary>Gets or sets what it can do, such as voice and sms.</summary>
    public IList<string> Features { get; set; } = [];
}

/// <summary>
/// One of the account's numbers.
/// </summary>
public sealed class TelnyxOwnedNumber
{
    /// <summary>Gets or sets the Telnyx id.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the number.</summary>
    public string PhoneNumber { get; set; }

    /// <summary>Gets or sets its status, such as active.</summary>
    public string Status { get; set; }

    /// <summary>Gets or sets the voice connection it rings.</summary>
    public string ConnectionId { get; set; }

    /// <summary>Gets or sets the messaging profile it texts through.</summary>
    public string MessagingProfileId { get; set; }

    /// <summary>
    /// Gets or sets how many calls from the number carriers refused as spam (SIP 607 or 608) in the last 30 days.
    /// </summary>
    public int SpamRejections { get; set; }

    /// <summary>
    /// Gets or sets when a carrier last refused a call from the number as spam.
    /// </summary>
    public DateTime? LastSpamRejectionUtc { get; set; }
}
