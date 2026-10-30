using System.Net;
using System.Net.Sockets;
using CrestApps.OrchardCore.Calendars.Core.External;
using CrestApps.OrchardCore.Calendars.Core.ICalendar;
using CrestApps.OrchardCore.Calendars.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Calendars.External;

/// <summary>
/// Subscribes to an iCalendar address (a <c>.ics</c> feed, such as the secret address Google Calendar or Outlook gives a
/// calendar). Read only: the calendar shows the feed's events and can't change them. Addresses on the local network are refused.
/// </summary>
public sealed class ICalendarUrlProvider : ICalendarProvider
{
    /// <summary>
    /// The provider's technical name.
    /// </summary>
    public const string ProviderName = "ics";

    private const int MaxBytes = 10 * 1024 * 1024;

    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ICalendarUrlProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public ICalendarUrlProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public string Name => ProviderName;

    /// <inheritdoc />
    public LocalizationSource DisplayName { get; } = LocalizationSource.Create<ICalendarUrlProvider>("iCalendar address (.ics)");

    /// <inheritdoc />
    public async Task<IReadOnlyList<ICalendarEventData>> PullAsync(Calendar calendar, CalendarSubscription subscription, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(subscription);

        var url = subscription.Url?.Trim() ?? string.Empty;

        if (url.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase))
        {
            url = string.Concat("https://", url.AsSpan(9));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("The address must start with https://, http:// or webcal://.");
        }

        await EnsurePublicAsync(uri, cancellationToken);

        using var client = _httpClientFactory.CreateClient(nameof(ICalendarUrlProvider));

        client.Timeout = TimeSpan.FromSeconds(30);

        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength > MaxBytes)
        {
            throw new InvalidOperationException("The calendar is larger than 10 MB.");
        }

        var text = await response.Content.ReadAsStringAsync(cancellationToken);

        if (text.Length > MaxBytes)
        {
            throw new InvalidOperationException("The calendar is larger than 10 MB.");
        }

        if (!text.Contains("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The address doesn't return an iCalendar document.");
        }

        return ICalendarReader.Read(text, calendar.TimeZoneId ?? "UTC");
    }

    private static async Task EnsurePublicAsync(Uri uri, CancellationToken cancellationToken)
    {
        var addresses = IPAddress.TryParse(uri.Host, out var literal) ? [literal] : await Dns.GetHostAddressesAsync(uri.Host, cancellationToken);

        foreach (var address in addresses)
        {
            if (IsPrivate(address))
            {
                throw new InvalidOperationException("Addresses on the local network can't be subscribed to.");
            }
        }
    }

    internal static bool IsPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal;
        }

        var bytes = address.GetAddressBytes();

        return bytes[0] == 10 ||
            (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
            (bytes[0] == 192 && bytes[1] == 168) ||
            (bytes[0] == 169 && bytes[1] == 254) ||
            (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) ||
            bytes[0] == 0;
    }
}
