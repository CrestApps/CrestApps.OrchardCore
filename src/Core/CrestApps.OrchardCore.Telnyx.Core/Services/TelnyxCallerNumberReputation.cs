using System.Globalization;
using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Documents;
using OrchardCore.Documents;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Counts the outbound calls carriers refused as spam, per caller number.
/// </summary>
/// <remarks>
/// A carrier whose call analytics label a number as spam answers its calls with SIP 608 Rejected, and a person who
/// declined it as unwanted with SIP 607 Unwanted. Either says the number's reputation is suffering: the calls the AI or
/// an agent places from it are not reaching anyone, and the number may need registering with the carriers' analytics
/// or replacing. The counts are kept per day for <see cref="WindowDays"/> days.
/// </remarks>
public sealed class TelnyxCallerNumberReputation
{
    /// <summary>
    /// The days the daily counts are kept for.
    /// </summary>
    public const int WindowDays = 30;

    private const string DayFormat = "yyyy-MM-dd";

    private readonly IDocumentManager<TelnyxCallerNumberReputationDocument> _documentManager;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxCallerNumberReputation"/> class.
    /// </summary>
    /// <param name="documentManager">The document holding the counts.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public TelnyxCallerNumberReputation(
        IDocumentManager<TelnyxCallerNumberReputationDocument> documentManager,
        IClock clock,
        ILogger<TelnyxCallerNumberReputation> logger)
    {
        _documentManager = documentManager;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Gets whether a call event is a carrier's spam rejection of a call placed from one of the account's numbers.
    /// </summary>
    /// <param name="callEvent">The call event.</param>
    /// <returns><see langword="true"/> when it is an outbound leg that ended with SIP 607 or 608.</returns>
    public static bool IsSpamRejection(TelnyxCallEvent callEvent)
        => callEvent is not null &&
            string.Equals(callEvent.EventType?.Trim(), "call.hangup", StringComparison.OrdinalIgnoreCase) &&
            callEvent.Direction?.Trim().ToLowerInvariant() is "outgoing" or "outbound" &&
            callEvent.SipHangupCause?.Trim() is "607" or "608" &&
            IsPhoneNumber(callEvent.From) &&
            IsPhoneNumber(callEvent.To);

    /// <summary>
    /// Counts the call event against its caller number when it is a spam rejection.
    /// </summary>
    /// <param name="callEvent">The call event.</param>
    /// <returns><see langword="true"/> when it was counted.</returns>
    public async Task<bool> ObserveAsync(TelnyxCallEvent callEvent)
    {
        if (!IsSpamRejection(callEvent))
        {
            return false;
        }

        var number = callEvent.From.Trim();
        var sipCode = callEvent.SipHangupCause.Trim();
        var now = callEvent.OccurredUtc ?? _clock.UtcNow;

        var document = await _documentManager.GetOrCreateMutableAsync();
        var rejections = Record(document, number, sipCode, now);
        await _documentManager.UpdateAsync(document);

        _logger.LogWarning(
            "A carrier rejected a call from {CallerNumber} as spam (SIP {SipCode}); {Count} rejections in the last {Days} days.",
            number.SanitizeLogValue(),
            sipCode.SanitizeLogValue(),
            rejections.CountSince(now.AddDays(-WindowDays)),
            WindowDays);

        return true;
    }

    /// <summary>
    /// Gets the counts per caller number.
    /// </summary>
    /// <returns>The counts, keyed by the number.</returns>
    public async Task<IReadOnlyDictionary<string, TelnyxCallerNumberRejections>> GetAsync()
        => (await _documentManager.GetOrCreateImmutableAsync()).Numbers;

    /// <summary>
    /// Copies each number's rejections in the window onto the numbers listed from the account.
    /// </summary>
    /// <param name="numbers">The account's numbers.</param>
    /// <returns>A task that completes when they are copied.</returns>
    public async Task ApplyToAsync(IEnumerable<TelnyxOwnedNumber> numbers)
    {
        ArgumentNullException.ThrowIfNull(numbers);

        var counts = await GetAsync();
        var since = _clock.UtcNow.AddDays(-WindowDays);

        foreach (var number in numbers)
        {
            if (number?.PhoneNumber is not null && counts.TryGetValue(number.PhoneNumber.Trim(), out var rejections))
            {
                number.SpamRejections = rejections.CountSince(since);
                number.LastSpamRejectionUtc = rejections.LastRejectedUtc;
            }
        }
    }

    internal static TelnyxCallerNumberRejections Record(TelnyxCallerNumberReputationDocument document, string number, string sipCode, DateTime nowUtc)
    {
        if (!document.Numbers.TryGetValue(number, out var rejections))
        {
            rejections = new TelnyxCallerNumberRejections();
            document.Numbers[number] = rejections;
        }

        var day = nowUtc.ToString(DayFormat, CultureInfo.InvariantCulture);
        rejections.Days[day] = rejections.Days.GetValueOrDefault(day) + 1;
        rejections.Total++;
        rejections.LastSipCode = sipCode;

        if (rejections.LastRejectedUtc is null || rejections.LastRejectedUtc < nowUtc)
        {
            rejections.LastRejectedUtc = nowUtc;
        }

        // The days that fell out of the window are dropped, so the document does not grow with every day a number calls.
        var oldest = nowUtc.Date.AddDays(-WindowDays).ToString(DayFormat, CultureInfo.InvariantCulture);

        foreach (var stale in rejections.Days.Keys.Where(key => string.CompareOrdinal(key, oldest) < 0).ToList())
        {
            rejections.Days.Remove(stale);
        }

        return rejections;
    }

    private static bool IsPhoneNumber(string value)
    {
        var trimmed = value?.Trim();

        return !string.IsNullOrEmpty(trimmed) &&
            trimmed.TrimStart('+').Length >= 7 &&
            trimmed.TrimStart('+').All(char.IsAsciiDigit);
    }
}

/// <summary>
/// The spam rejections counted per caller number.
/// </summary>
public sealed class TelnyxCallerNumberReputationDocument : Document
{
    /// <summary>
    /// Gets the rejections, keyed by the caller number.
    /// </summary>
    public Dictionary<string, TelnyxCallerNumberRejections> Numbers { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>
/// The spam rejections of one caller number.
/// </summary>
public sealed class TelnyxCallerNumberRejections
{
    /// <summary>
    /// Gets or sets the rejections counted since the number was first rejected.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Gets the rejections per UTC day (<c>yyyy-MM-dd</c>), for the days in the window.
    /// </summary>
    public Dictionary<string, int> Days { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets when the number was last rejected.
    /// </summary>
    public DateTime? LastRejectedUtc { get; set; }

    /// <summary>
    /// Gets or sets the SIP answer of the last rejection: 607 Unwanted or 608 Rejected.
    /// </summary>
    public string LastSipCode { get; set; }

    /// <summary>
    /// Counts the rejections on or after a UTC day.
    /// </summary>
    /// <param name="sinceUtc">The first moment counted; its whole day is counted.</param>
    /// <returns>The rejections.</returns>
    public int CountSince(DateTime sinceUtc)
    {
        var first = sinceUtc.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return Days.Where(day => string.CompareOrdinal(day.Key, first) >= 0).Sum(day => day.Value);
    }
}
