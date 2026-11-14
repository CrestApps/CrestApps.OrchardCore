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
/// egress-guarded client, which refuses the site's own network at connect, after every DNS answer and on every redirect
/// it follows; the body is read up to 10 MB and no further, and the whole fetch, body included, within 30 seconds. A
/// fetch that runs out of time fails with a <see cref="TimeoutException"/>, so the sync records it on this subscription
/// and goes on with the next calendar; only the caller's own cancellation stops the run.
/// </summary>
public sealed class ICalendarUrlProvider : ICalendarProvider
{
    /// <summary>
    /// The provider's technical name.
    /// </summary>
    public const string ProviderName = "ics";

    private const int MaxBytes = 10 * 1024 * 1024;

    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="ICalendarUrlProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public ICalendarUrlProvider(IHttpClientFactory httpClientFactory)
        : this(httpClientFactory, _defaultTimeout)
    {
    }

    internal ICalendarUrlProvider(IHttpClientFactory httpClientFactory, TimeSpan timeout)
    {
        _httpClientFactory = httpClientFactory;
        _timeout = timeout;
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

        // HttpClient.Timeout stops at the headers when the body is streamed, and the sync task reads one feed after another,
        // so a feed that trickles its body would hold up every other calendar: the timeout covers the body too.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);

        string text;

        try
        {
            text = await FetchAsync(uri, timeout.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // The sync run treats a cancellation as its own and stops; running out of time is this feed's failure.
            throw new TimeoutException("The calendar didn't answer in time.", ex);
        }

        if (!text.Contains("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The address doesn't return an iCalendar document.");
        }

        return ICalendarReader.Read(text, calendar.TimeZoneId ?? "UTC");
    }

    private async Task<string> FetchAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient(nameof(ICalendarUrlProvider));
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadBoundedAsync(MaxBytes, cancellationToken)
            ?? throw new InvalidOperationException("The calendar is larger than 10 MB.");

        return Decode(bytes, response.Content.Headers.ContentType?.CharSet);
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
