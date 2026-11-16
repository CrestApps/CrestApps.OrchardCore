using System.Globalization;
using CrestApps.OrchardCore.Learning.Packages.Core.Scorm;
using CrestApps.OrchardCore.Learning.Packages.Models;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Services;

/// <summary>
/// What the package panel, the runtime endpoint and the index show of a runtime state of either standard: one status, the
/// score as a percentage, the total time and whether the next launch resumes.
/// </summary>
/// <param name="Status">The status: the SCORM 1.2 lesson status; for SCORM 2004 the evaluated success status when it
/// is <c>passed</c> or <c>failed</c>, else the evaluated completion status (<c>completed</c>, <c>incomplete</c>,
/// <c>not attempted</c> or <c>unknown</c>); <see langword="null"/> without a state.</param>
/// <param name="Score">The score as a percentage, or <see langword="null"/>.</param>
/// <param name="TotalTime">The total time to show (the CMITimespan for SCORM 1.2, hours, minutes and seconds for SCORM
/// 2004), or <see langword="null"/> when no time was recorded.</param>
/// <param name="Resumes">Whether the content suspended its session, so the next launch resumes it.</param>
public sealed record PackageStateSummary(string Status, decimal? Score, string TotalTime, bool Resumes)
{
    /// <summary>
    /// Summarizes a runtime state.
    /// </summary>
    /// <param name="state">The state, or <see langword="null"/>.</param>
    /// <returns>The summary.</returns>
    public static PackageStateSummary Of(PackageRuntimeState state)
    {
        if (state is null)
        {
            return new PackageStateSummary(null, null, null, false);
        }

        if (string.Equals(state.Standard, PackagesConstants.Standards.Cmi5, StringComparison.Ordinal))
        {
            var cmi5 = state.Cmi5 ?? new Cmi5State();
            var status = cmi5.CourseSatisfied
                ? cmi5.Aus.Any(au => au.Passed) ? "passed" : "completed"
                : cmi5.Aus.Any(au => au.Failed && !au.Passed) ? "failed" : state.Sessions > 0 ? "incomplete" : null;
            var scores = cmi5.Aus.Where(au => au.ScoreScaled is not null).Select(au => au.ScoreScaled.Value).ToList();
            decimal? score = scores.Count == 0 ? null : Math.Round(Math.Clamp(scores.Average() * 100, 0, 100), 2, MidpointRounding.AwayFromZero);

            return new PackageStateSummary(status, score, null, false);
        }

        if (PackagesConstants.Standards.IsScorm2004(state.Standard))
        {
            var cmi = state.Cmi2004 ?? new Scorm2004Cmi();
            var success = Scorm2004DataModel.EvaluateSuccessStatus(cmi);
            var status = success is "passed" or "failed" ? success : Scorm2004DataModel.EvaluateCompletionStatus(cmi);
            var time = Scorm2004Rules.TryParseDuration(cmi.TotalTime, out var total) && total > TimeSpan.Zero
                ? string.Create(CultureInfo.InvariantCulture, $"{(long)total.TotalHours}:{total.Minutes:00}:{total.Seconds:00}")
                : null;

            return new PackageStateSummary(status, PackageResultMapper.ScorePercent(cmi), time, string.Equals(cmi.Exit, "suspend", StringComparison.Ordinal));
        }

        var scorm12 = state.Cmi ?? new Scorm12Cmi();
        var hasTime = Scorm12Rules.TryParseTimespan(scorm12.TotalTime, out var spent) && spent > TimeSpan.Zero;

        return new PackageStateSummary(scorm12.LessonStatus, PackageResultMapper.ScorePercent(scorm12), hasTime ? scorm12.TotalTime : null, string.Equals(scorm12.Exit, "suspend", StringComparison.Ordinal));
    }
}
