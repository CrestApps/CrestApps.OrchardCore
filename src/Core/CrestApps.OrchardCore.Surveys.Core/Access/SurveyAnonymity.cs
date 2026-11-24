using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// The rules every output of an anonymous survey follows: the minimum group size, with the site's floor, and times shown
/// by the day only.
/// </summary>
public static class SurveyAnonymity
{
    /// <summary>
    /// The id an anonymous response keeps in place of the id of a context that can point to one person.
    /// </summary>
    public const string HiddenContextId = "anonymous";

    /// <summary>
    /// Gets whether a survey is anonymous.
    /// </summary>
    /// <param name="survey">The survey, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it records nobody.</returns>
    public static bool IsAnonymous(Survey survey)
        => survey?.Access?.Anonymity == AnonymityMode.Anonymous;

    /// <summary>
    /// Gets the floor of the minimum group size the site sets, at least 1.
    /// </summary>
    /// <param name="settings">The site settings, or <see langword="null"/>.</param>
    /// <returns>The floor.</returns>
    public static int Floor(SurveySettings settings)
        => Math.Max(1, settings?.MinimumGroupSizeFloor ?? SurveySettings.DefaultMinimumGroupSizeFloor);

    /// <summary>
    /// Gets the fewest responses an output of a survey may show: the survey's own minimum, never below the site's floor,
    /// or 0 for an identified survey.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="settings">The site settings, or <see langword="null"/>.</param>
    /// <returns>The minimum, or 0 when there is none.</returns>
    public static int MinimumGroupSize(Survey survey, SurveySettings settings)
        => IsAnonymous(survey) ? Math.Max(survey.Access?.MinimumGroupSize ?? 0, Floor(settings)) : 0;

    /// <summary>
    /// Gets whether a group of responses is too small to show.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="settings">The site settings, or <see langword="null"/>.</param>
    /// <param name="count">How many responses the group holds.</param>
    /// <returns><see langword="true"/> when the group must be hidden.</returns>
    public static bool IsTooSmall(Survey survey, SurveySettings settings, long count)
    {
        var minimum = MinimumGroupSize(survey, settings);

        return minimum > 0 && count < minimum;
    }

    /// <summary>
    /// Gets the context a response of a survey keeps: on an anonymous survey, a context whose kind can point to one person
    /// (an enrollment, an invitation's recipient, or any kind not registered as shared) keeps its kind only.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="context">The context asked for, or <see langword="null"/>.</param>
    /// <param name="kinds">The registered context kinds, or <see langword="null"/>.</param>
    /// <returns>The context to keep.</returns>
    public static RecordRef? StoredContext(Survey survey, RecordRef? context, SurveyContextKindOptions kinds)
    {
        if (context is not { IsEmpty: false } value || !IsAnonymous(survey))
        {
            return context;
        }

        return kinds?.Kinds.TryGetValue(value.Kind, out var kind) == true && kind is { IdentifiesRespondent: false }
            ? value
            : new RecordRef(value.Kind, HiddenContextId);
    }

    /// <summary>
    /// The characters of an anonymous response's label.
    /// </summary>
    public const int AnonymousLabelLength = 10;

    // Crockford's base 32, lower case: no i, l, o or u, so a label read aloud or typed back is unambiguous.
    private const string LabelAlphabet = "0123456789abcdefghjkmnpqrstvwxyz";

    /// <summary>
    /// Gets the label staff see for a response: its number on an identified survey. Response numbers come from one
    /// sequence for the whole site, in the order responses start, so on an anonymous survey a number next to a named
    /// response's number would name the person; an anonymous response is labeled instead with a keyed hash of its id
    /// under the survey's anonymity secret (<see cref="AnonymousLabel"/>). The label orders nothing, matches nothing on
    /// another survey, and can't be worked out from the id that the response's links (and so the server's access logs)
    /// carry.
    /// </summary>
    /// <param name="survey">The survey, or <see langword="null"/> when unknown (treated as anonymous).</param>
    /// <param name="response">The response.</param>
    /// <param name="secret">The survey's anonymity secret in plain text (<see cref="SurveyAnonymitySecrets.RevealAsync"/>);
    /// read only for an anonymous survey.</param>
    /// <returns>The label.</returns>
    public static string ResponseLabel(Survey survey, SurveyResponse response, string secret)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (survey is not null && !IsAnonymous(survey))
        {
            return string.IsNullOrEmpty(response.Number) ? response.ItemId ?? string.Empty : response.Number;
        }

        return AnonymousLabel(secret, response.ItemId);
    }

    /// <summary>
    /// Gets the label of an anonymous response: the first characters of an HMAC-SHA256 of its id under the survey's
    /// anonymity secret, in lower-case base 32. Without the secret, the label is empty.
    /// </summary>
    /// <param name="secret">The survey's anonymity secret in plain text.</param>
    /// <param name="responseId">The id of the response.</param>
    /// <returns>The label, or an empty string.</returns>
    public static string AnonymousLabel(string secret, string responseId)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(responseId))
        {
            return string.Empty;
        }

        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes("response-label:" + responseId));
        var label = new StringBuilder(AnonymousLabelLength);
        var buffer = 0;
        var bits = 0;

        foreach (var value in hash)
        {
            buffer = (buffer << 8) | value;
            bits += 8;

            while (bits >= 5 && label.Length < AnonymousLabelLength)
            {
                bits -= 5;
                label.Append(LabelAlphabet[(buffer >> bits) & 31]);
            }

            if (label.Length == AnonymousLabelLength)
            {
                break;
            }
        }

        return label.ToString();
    }

    /// <summary>
    /// Widens a time range to whole days in a time zone: the start moves back to the start of its day, and the end
    /// (excluded) forward to the start of the next day unless it already falls on a midnight. A filter on the times of an
    /// anonymous survey's responses is widened this way before it reaches a query, so stepping its time of day can't
    /// tell, minute by minute, when a response came in: its times are shown by the day only.
    /// </summary>
    /// <param name="fromUtc">The start of the range, in UTC, or <see langword="null"/>.</param>
    /// <param name="toUtc">The end of the range (excluded), in UTC, or <see langword="null"/>.</param>
    /// <param name="timeZone">The time zone the days are read in, or <see langword="null"/> for UTC.</param>
    /// <returns>The widened range.</returns>
    public static (DateTime? FromUtc, DateTime? ToUtc) WholeDays(DateTime? fromUtc, DateTime? toUtc, TimeZoneInfo timeZone)
    {
        DateTime? from = fromUtc is { } start && start > DateTime.MinValue.AddDays(2) ? StartOfDayUtc(start, timeZone) : fromUtc;
        DateTime? to = toUtc;

        if (toUtc is { } end && end < DateTime.MaxValue.AddDays(-2))
        {
            var day = StartOfDayUtc(end, timeZone);

            to = day == DateTime.SpecifyKind(end, DateTimeKind.Utc) ? day : EndOfDayUtc(end, timeZone);
        }

        return (from, to);
    }

    /// <summary>
    /// Gets the end of the day a time falls on in a time zone (the start of the next day), as UTC.
    /// </summary>
    /// <param name="utc">The time, in UTC.</param>
    /// <param name="timeZone">The time zone the day is read in, or <see langword="null"/> for UTC.</param>
    /// <returns>The start of the next day, in UTC.</returns>
    public static DateTime EndOfDayUtc(DateTime utc, TimeZoneInfo timeZone)
    {
        var zone = timeZone ?? TimeZoneInfo.Utc;
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);

        return CalendarTime.ToUtc(local.Date.AddDays(1), zone.Id);
    }

    /// <summary>
    /// Gets a time of an anonymous response shown by the day only: the start of its day in a time zone, as UTC.
    /// </summary>
    /// <param name="utc">The time, in UTC.</param>
    /// <param name="timeZone">The time zone the day is read in, or <see langword="null"/> for UTC.</param>
    /// <returns>The start of the day, in UTC.</returns>
    public static DateTime StartOfDayUtc(DateTime utc, TimeZoneInfo timeZone)
    {
        var zone = timeZone ?? TimeZoneInfo.Utc;
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);

        // A midnight a daylight-saving change skips starts the day when the clocks move forward.
        return CalendarTime.ToUtc(local.Date, zone.Id);
    }
}
