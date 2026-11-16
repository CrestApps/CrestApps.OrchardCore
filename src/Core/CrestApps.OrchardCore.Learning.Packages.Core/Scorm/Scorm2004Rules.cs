using System.Globalization;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Learning.Packages.Models;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Scorm;

/// <summary>
/// What the LMS does with the SCORM 2004 data around a session: a launch resumes the attempt after <c>cmi.exit</c>
/// <c>suspend</c> and otherwise starts a new attempt with its data reset; a terminated session adds its
/// <c>cmi.session_time</c> to <c>cmi.total_time</c>. Times are ISO 8601 durations (<c>timeinterval(second,10,2)</c>).
/// </summary>
public static partial class Scorm2004Rules
{
    /// <summary>The total time of a new attempt.</summary>
    public const string ZeroDuration = "PT0H0M0S";

    /// <summary>
    /// Starts a session. The first session, and every session after an attempt that ended (an exit other than
    /// <c>suspend</c>: <c>normal</c>, <c>logout</c>, <c>time-out</c> or none), starts a new attempt: entry
    /// <c>ab-initio</c>, and the attempt's data reset (location, suspend data, statuses, scores, progress, total time,
    /// interactions, the learner's comments, and the objectives back to the manifest's ids). After <c>suspend</c> the
    /// attempt resumes with its data and entry <c>resume</c>. Either way the exit, the session time and the navigation
    /// request of the earlier session are cleared. The learner's preferences, the comments from the LMS and the edition
    /// are kept: they are not attempt data.
    /// </summary>
    /// <param name="cmi">The data.</param>
    /// <param name="firstSession">Whether the learner never launched the package before.</param>
    /// <param name="objectiveIds">The ids of the objectives the manifest gives the SCO.</param>
    /// <returns><see langword="true"/> when a new attempt started.</returns>
    public static bool BeginSession(Scorm2004Cmi cmi, bool firstSession, IEnumerable<string> objectiveIds)
    {
        ArgumentNullException.ThrowIfNull(cmi);

        var resume = !firstSession && string.Equals(cmi.Exit, "suspend", StringComparison.Ordinal);

        if (!resume)
        {
            cmi.Location = null;
            cmi.SuspendData = null;
            cmi.CompletionStatus = "unknown";
            cmi.SuccessStatus = "unknown";
            cmi.ProgressMeasure = null;
            cmi.ScoreScaled = null;
            cmi.ScoreRaw = null;
            cmi.ScoreMin = null;
            cmi.ScoreMax = null;
            cmi.TotalTime = ZeroDuration;
            cmi.Interactions = [];
            cmi.CommentsFromLearner = [];
            cmi.Objectives = (objectiveIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Select(id => new Scorm2004Objective { Id = id }).ToList();
        }

        cmi.Entry = resume ? "resume" : "ab-initio";
        cmi.Exit = string.Empty;
        cmi.SessionTime = null;
        cmi.NavRequest = "_none_";

        return !resume;
    }

    /// <summary>
    /// Terminates a session: adds the session time to the total time of the attempt once.
    /// </summary>
    /// <param name="cmi">The data.</param>
    public static void FinishSession(Scorm2004Cmi cmi)
    {
        ArgumentNullException.ThrowIfNull(cmi);

        if (!string.IsNullOrEmpty(cmi.SessionTime))
        {
            cmi.TotalTime = AddDuration(cmi.TotalTime, cmi.SessionTime);
            cmi.SessionTime = null;
        }
    }

    /// <summary>
    /// Adds two ISO 8601 durations.
    /// </summary>
    /// <param name="total">The total.</param>
    /// <param name="add">The time to add; ignored when it is not a duration.</param>
    /// <returns>The sum as hours, minutes and seconds, such as <c>PT1H2M3.5S</c>.</returns>
    public static string AddDuration(string total, string add)
    {
        var sum = TryParseDuration(total, out var current) ? current : TimeSpan.Zero;

        if (TryParseDuration(add, out var extra))
        {
            sum += extra;
        }

        return FormatDuration(sum);
    }

    /// <summary>
    /// Formats a time as an ISO 8601 duration of hours, minutes and seconds, to the hundredth of a second.
    /// </summary>
    /// <param name="time">The time.</param>
    /// <returns>The duration, such as <c>PT25H0M0S</c>.</returns>
    public static string FormatDuration(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }

        var hundredths = (long)Math.Round(time.Ticks / (decimal)TimeSpan.TicksPerMillisecond / 10, MidpointRounding.AwayFromZero);
        var hours = hundredths / 360_000;
        var minutes = hundredths / 6_000 % 60;
        var seconds = hundredths % 6_000 / 100m;

        return string.Create(CultureInfo.InvariantCulture, $"PT{hours}H{minutes}M{seconds.ToString("0.##", CultureInfo.InvariantCulture)}S");
    }

    /// <summary>
    /// Reads an ISO 8601 duration. A year is counted as 365 days and a month as 30 days.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="time">The time.</param>
    /// <returns><see langword="true"/> when it is a duration.</returns>
    public static bool TryParseDuration(string value, out TimeSpan time)
    {
        time = TimeSpan.Zero;

        if (!IsDuration(value))
        {
            return false;
        }

        var match = DurationPattern().Match(value);

        static decimal Part(Match match, string name)
            => match.Groups[name].Success ? decimal.Parse(match.Groups[name].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture) : 0;

        var seconds = (Part(match, "y") * 365 + Part(match, "mo") * 30 + Part(match, "d")) * 86_400
            + Part(match, "h") * 3_600
            + Part(match, "mi") * 60
            + Part(match, "s");

        if (seconds > (decimal)TimeSpan.MaxValue.TotalSeconds / 2)
        {
            return false;
        }

        time = TimeSpan.FromTicks((long)(seconds * TimeSpan.TicksPerSecond));

        return true;
    }

    /// <summary>
    /// Gets whether a value is an ISO 8601 duration: <c>P[nY][nM][nD][T[nH][nM][n[.n]S]]</c> with at least one part, a
    /// time part after <c>T</c>, and a fraction on the seconds only.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public static bool IsDuration(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 64)
        {
            return false;
        }

        var match = DurationPattern().Match(value);

        if (!match.Success)
        {
            return false;
        }

        var hasDate = match.Groups["y"].Success || match.Groups["mo"].Success || match.Groups["d"].Success;
        var hasTime = match.Groups["h"].Success || match.Groups["mi"].Success || match.Groups["s"].Success;

        return (hasDate || hasTime) && (!match.Groups["t"].Success || hasTime);
    }

    [GeneratedRegex("^P(?:(?<y>[0-9]{1,10})Y)?(?:(?<mo>[0-9]{1,10})M)?(?:(?<d>[0-9]{1,10})D)?(?<t>T(?:(?<h>[0-9]{1,10})H)?(?:(?<mi>[0-9]{1,10})M)?(?:(?<s>[0-9]{1,10}(?:\\.[0-9]{1,10})?)S)?)?$", RegexOptions.CultureInvariant)]
    private static partial Regex DurationPattern();
}
