using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// What the Contact Center operations tools (recordings, shared voicemail, live supervision) share: the caller's user
/// id, customer numbers cut to their last digits for lists, matching a number the model gives, local times read from
/// the model, and the outcome of a supervisor's action.
/// </summary>
internal static class ContactCenterOperationsSupport
{
    private static readonly string[] _localFormats =
    [
        "yyyy-MM-dd'T'HH:mm",
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd HH:mm:ss",
    ];

    /// <summary>
    /// Gets the caller's user identifier.
    /// </summary>
    /// <param name="user">The caller.</param>
    /// <returns>The identifier, or <see langword="null"/> when the principal does not carry one.</returns>
    public static string GetUserId(ClaimsPrincipal user)
    {
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        return string.IsNullOrEmpty(userId) ? null : userId;
    }

    /// <summary>
    /// Gets the last four digits of a phone number, which is all a list shows of a customer's number.
    /// </summary>
    /// <param name="number">The number.</param>
    /// <returns>The last four digits, or <see langword="null"/>.</returns>
    public static string Ending(string number)
    {
        var digits = Digits(number);

        return digits.Length == 0
            ? null
            : digits.Length <= 4 ? digits : digits[^4..];
    }

    /// <summary>
    /// Determines whether a stored number is the number the model asked about: the same digits, or, for a number given
    /// without its country code, the same last digits. At least four digits must be given.
    /// </summary>
    /// <param name="stored">The stored number.</param>
    /// <param name="requested">The number the model gave.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    public static bool MatchesNumber(string stored, string requested)
    {
        var wanted = Digits(requested);
        var candidate = Digits(stored);

        return wanted.Length >= 4 &&
            candidate.Length > 0 &&
            candidate.EndsWith(wanted, StringComparison.Ordinal);
    }

    /// <summary>
    /// Reads a date and time the model gave in the user's time zone.
    /// </summary>
    /// <param name="text">The text, for example <c>2026-11-18T14:30</c> or <c>2026-11-18</c>.</param>
    /// <param name="value">The local date and time, truncated to the minute.</param>
    /// <param name="dateOnly">Whether only a date was given.</param>
    /// <returns><see langword="false"/> when the text is not a date.</returns>
    public static bool TryParseLocal(string text, out DateTime value, out bool dateOnly)
    {
        value = default;
        dateOnly = false;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        text = text.Trim();

        if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            value = DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
            dateOnly = true;

            return true;
        }

        if (!DateTime.TryParseExact(text, _localFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return false;
        }

        value = new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute, 0, DateTimeKind.Unspecified);

        return true;
    }

    /// <summary>
    /// Describes what became of a supervisor's action on a call or an agent.
    /// </summary>
    /// <param name="result">The result of the supervision service.</param>
    /// <param name="done">What was done, said when the action succeeded and the service added nothing.</param>
    /// <returns>The result for the model.</returns>
    public static string Outcome(SupervisorEngagementResult result, string done)
    {
        if (result is null)
        {
            return ContactCenterSetupToolBase.Json(new { succeeded = false, message = "Nothing was done: the action is not available." });
        }

        return ContactCenterSetupToolBase.Json(new
        {
            succeeded = result.Succeeded,
            outcomeUnknown = result.OutcomeUnknown,
            message = result.Succeeded
                ? (string.IsNullOrWhiteSpace(result.Reason) ? done : $"{done} {result.Reason}")
                : result.OutcomeUnknown
                    ? $"It is not known whether it worked: {result.Reason} Check the call before trying again."
                    : $"Nothing was done: {result.Reason ?? "the action was refused."}",
        });
    }

    private static string Digits(string number)
        => string.IsNullOrWhiteSpace(number)
            ? string.Empty
            : new string(number.Where(char.IsAsciiDigit).ToArray());
}
