using System.Globalization;
using CrestApps.OrchardCore.Learning.Packages.Models;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Scorm;

/// <summary>
/// What the LMS does with the SCORM 1.2 data around a session: the entry of a new session, the lesson status from the
/// mastery score, and the total time when a session finishes.
/// </summary>
public static class Scorm12Rules
{
    private static readonly TimeSpan _maxTimespan = new TimeSpan(9999, 59, 59) + TimeSpan.FromMilliseconds(990);

    /// <summary>
    /// Starts a session: the entry is <c>ab-initio</c> the first time, <c>resume</c> after a session that exited with
    /// <c>suspend</c>, and empty otherwise; the exit and session time of the earlier session are cleared.
    /// </summary>
    /// <param name="cmi">The data.</param>
    /// <param name="firstSession">Whether the learner never launched the package before.</param>
    public static void BeginSession(Scorm12Cmi cmi, bool firstSession)
    {
        ArgumentNullException.ThrowIfNull(cmi);

        cmi.Entry = firstSession ? "ab-initio" : string.Equals(cmi.Exit, "suspend", StringComparison.Ordinal) ? "resume" : string.Empty;
        cmi.Exit = string.Empty;
        cmi.SessionTime = string.Empty;
    }

    /// <summary>
    /// Sets the lesson status from the mastery score when the content sent a raw score without passing or failing the
    /// learner itself: passed when the raw score reaches the mastery score, failed otherwise. It applies to a status of
    /// <c>not attempted</c>, <c>completed</c> or <c>browsed</c>; the content's own <c>passed</c> or <c>failed</c> stands,
    /// and so does <c>incomplete</c>, which says the learner has not finished. Nothing changes without a mastery score,
    /// without a numeric raw score, or in <c>no-credit</c>.
    /// </summary>
    /// <param name="cmi">The data.</param>
    /// <returns><see langword="true"/> when the status changed.</returns>
    public static bool ApplyMasteryScore(Scorm12Cmi cmi)
    {
        ArgumentNullException.ThrowIfNull(cmi);

        if (!string.Equals(cmi.Credit, "credit", StringComparison.Ordinal) ||
            cmi.LessonStatus is not ("not attempted" or "completed" or "browsed") ||
            !TryParseNumber(cmi.MasteryScore, out var mastery) ||
            !TryParseNumber(cmi.ScoreRaw, out var raw))
        {
            return false;
        }

        var status = raw >= mastery ? "passed" : "failed";
        var changed = !string.Equals(status, cmi.LessonStatus, StringComparison.Ordinal);

        cmi.LessonStatus = status;

        return changed;
    }

    /// <summary>
    /// Finishes a session: applies the mastery score, sets a status the content never set (<c>not attempted</c>) to
    /// <c>completed</c>, and adds the session time to the total time once.
    /// </summary>
    /// <param name="cmi">The data.</param>
    public static void FinishSession(Scorm12Cmi cmi)
    {
        ArgumentNullException.ThrowIfNull(cmi);

        ApplyMasteryScore(cmi);

        if (string.Equals(cmi.LessonStatus, "not attempted", StringComparison.Ordinal))
        {
            cmi.LessonStatus = "completed";
        }

        if (!string.IsNullOrEmpty(cmi.SessionTime))
        {
            cmi.TotalTime = AddTimespan(cmi.TotalTime, cmi.SessionTime);
            cmi.SessionTime = string.Empty;
        }
    }

    /// <summary>
    /// Adds two CMITimespan values.
    /// </summary>
    /// <param name="total">The total, <c>HHHH:MM:SS.SS</c>.</param>
    /// <param name="add">The time to add.</param>
    /// <returns>The sum as <c>HHHH:MM:SS.SS</c>, at most <c>9999:59:59.99</c>.</returns>
    public static string AddTimespan(string total, string add)
    {
        var sum = TryParseTimespan(total, out var current) ? current : TimeSpan.Zero;

        if (TryParseTimespan(add, out var extra))
        {
            sum += extra;
        }

        if (sum > _maxTimespan)
        {
            sum = _maxTimespan;
        }

        var hours = (long)sum.TotalHours;
        var hundredths = sum.Milliseconds / 10;

        return string.Create(CultureInfo.InvariantCulture, $"{hours:0000}:{sum.Minutes:00}:{sum.Seconds:00}.{hundredths:00}");
    }

    /// <summary>
    /// Reads a CMITimespan.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="span">The time.</param>
    /// <returns><see langword="true"/> when it is a CMITimespan.</returns>
    public static bool TryParseTimespan(string value, out TimeSpan span)
    {
        span = TimeSpan.Zero;

        if (!Scorm12DataModel.IsTimespan(value))
        {
            return false;
        }

        var parts = value.Split(':');
        var hours = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var minutes = int.Parse(parts[1], CultureInfo.InvariantCulture);
        var seconds = decimal.Parse(parts[2], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

        span = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromMilliseconds((double)Math.Round(seconds * 1000));

        return true;
    }

    /// <summary>
    /// Reads a CMIDecimal.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="number">The number.</param>
    /// <returns><see langword="true"/> when it is a number.</returns>
    public static bool TryParseNumber(string value, out decimal number)
    {
        number = 0;

        return !string.IsNullOrWhiteSpace(value) &&
            decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number);
    }
}
