using CrestApps.OrchardCore.Learning.Coursework.Models;

namespace CrestApps.OrchardCore.Learning.Coursework.Core.Services;

/// <summary>
/// The late policy of coursework: work loses <see cref="CourseworkPart.LatePenaltyPercentPerDay"/> of its score for
/// each day, or part of a day, it came after the due date, up to <see cref="CourseworkPart.MaxLatePenaltyPercent"/>,
/// and the coursework stops taking work <see cref="CourseworkPart.CloseAfterDays"/> whole days after the due date.
/// </summary>
public static class CourseworkLatePolicy
{
    /// <summary>
    /// Gets when the coursework stops taking work: the due date plus the close days.
    /// </summary>
    /// <param name="part">The coursework part.</param>
    /// <returns>The close time in UTC, or <see langword="null"/> when it never closes.</returns>
    public static DateTime? GetCloseUtc(CourseworkPart part)
    {
        ArgumentNullException.ThrowIfNull(part);

        return part.DueUtc is { } due && part.CloseAfterDays is { } days && days >= 0
            ? DateTime.SpecifyKind(due, DateTimeKind.Utc).AddDays(days)
            : null;
    }

    /// <summary>
    /// Tells whether the coursework stopped taking work.
    /// </summary>
    /// <param name="part">The coursework part.</param>
    /// <param name="nowUtc">The time now, in UTC.</param>
    /// <returns><see langword="true"/> when the close time has passed.</returns>
    public static bool IsClosed(CourseworkPart part, DateTime nowUtc)
        => GetCloseUtc(part) is { } close && nowUtc > close;

    /// <summary>
    /// Counts the days work came after the due date, a part of a day counting as a whole day.
    /// </summary>
    /// <param name="dueUtc">The due date in UTC, or <see langword="null"/> for none.</param>
    /// <param name="submittedUtc">When the work came, in UTC.</param>
    /// <returns>The days late; zero for work on time or without a due date.</returns>
    public static int GetDaysLate(DateTime? dueUtc, DateTime submittedUtc)
    {
        if (dueUtc is not { } due)
        {
            return 0;
        }

        var late = DateTime.SpecifyKind(submittedUtc, DateTimeKind.Utc) - DateTime.SpecifyKind(due, DateTimeKind.Utc);

        if (late <= TimeSpan.Zero)
        {
            return 0;
        }

        return (int)Math.Min(int.MaxValue, (late.Ticks + TimeSpan.TicksPerDay - 1) / TimeSpan.TicksPerDay);
    }

    /// <summary>
    /// Computes the late penalty: the percentage a day times the days late, no more than the largest penalty.
    /// </summary>
    /// <param name="part">The coursework part.</param>
    /// <param name="daysLate">The days late.</param>
    /// <returns>The penalty, as a percentage of the score from 0 to 100.</returns>
    public static decimal GetPenaltyPercent(CourseworkPart part, int daysLate)
    {
        ArgumentNullException.ThrowIfNull(part);

        if (daysLate <= 0 || part.LatePenaltyPercentPerDay <= 0)
        {
            return 0;
        }

        return Math.Min(daysLate * part.LatePenaltyPercentPerDay, Math.Clamp(part.MaxLatePenaltyPercent, 0, 100));
    }

    /// <summary>
    /// Takes a late penalty off a score.
    /// </summary>
    /// <param name="rawScore">The score before the penalty, as a percentage.</param>
    /// <param name="penaltyPercent">The penalty, as a percentage of the score.</param>
    /// <returns>The final score, rounded to two decimals.</returns>
    public static decimal Apply(decimal rawScore, decimal penaltyPercent)
        => Math.Round(rawScore * (100 - Math.Clamp(penaltyPercent, 0, 100)) / 100, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Finds the days late and the penalty of a submission: the ones kept when it was graded, or else the ones the
    /// coursework's policy gives now.
    /// </summary>
    /// <param name="submission">The submission.</param>
    /// <param name="part">The coursework part, or <see langword="null"/> when the coursework is gone.</param>
    /// <returns>The days late and the penalty.</returns>
    public static (int DaysLate, decimal PenaltyPercent) Resolve(Submission submission, CourseworkPart part)
    {
        ArgumentNullException.ThrowIfNull(submission);

        if (submission.DaysLate is { } keptDays && submission.LatePenaltyPercent is { } keptPenalty)
        {
            return (keptDays, keptPenalty);
        }

        if (part is null)
        {
            return (0, 0);
        }

        var days = GetDaysLate(part.DueUtc, submission.SubmittedUtc);

        return (days, GetPenaltyPercent(part, days));
    }
}
