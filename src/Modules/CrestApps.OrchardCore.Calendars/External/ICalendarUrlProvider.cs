using System.Text;
using CrestApps.OrchardCore.Calendars.Core.External;
using CrestApps.OrchardCore.Calendars.Core.ICalendar;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Core.Http;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Calendars.External;

/// <summary>
/// Subscribes to an iCalendar address (a <c>.ics</c> feed, such as the secret address Google Calendar or Outlook gives a
/// calendar). Read only: the calendar shows the feed's events and can't change them. The feed is fetched with the
/// egress-guarded client, which refuses the site's own network at connect, after every DNS answer, and follows no
/// redirect; the body is read up to 10 MB and no further.
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

        using var client = _httpClientFactory.CreateClient(nameof(ICalendarUrlProvider));
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadBoundedAsync(MaxBytes, cancellationToken)
            ?? throw new InvalidOperationException("The calendar is larger than 10 MB.");

        var text = Decode(bytes, response.Content.Headers.ContentType?.CharSet);

        if (!text.Contains("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The address doesn't return an iCalendar document.");
        }

        return ICalendarReader.Read(text, calendar.TimeZoneId ?? "UTC");
    }

    // The declared character set when there is one and it is known, otherwise UTF-8 (iCalendar's default); a byte order
    // mark wins over both.
    private static string Decode(byte[] bytes, string charSet)
    {
        var encoding = Encoding.UTF8;

        if (!string.IsNullOrWhiteSpace(charSet))
        {
            try
            {
                encoding = Encoding.GetEncoding(charSet.Trim('"', ' '));
            }
            catch (ArgumentException)
            {
                // An unknown character set is read as UTF-8.
            }
        }

        using var reader = new StreamReader(new MemoryStream(bytes), encoding, detectEncodingFromByteOrderMarks: true);

        return reader.ReadToEnd();
    }
}
