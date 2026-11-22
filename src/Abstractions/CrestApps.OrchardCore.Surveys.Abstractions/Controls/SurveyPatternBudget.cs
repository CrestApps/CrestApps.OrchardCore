using System.Diagnostics;

namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// The time the pattern (regular expression) rules of one request may take together, shared by every answer the request
/// checks, so a page with many pattern questions can't cost more than one budget.
/// </summary>
public sealed class SurveyPatternBudget
{
    /// <summary>
    /// The default time the pattern rules of one request may take together.
    /// </summary>
    public static readonly TimeSpan DefaultPerRequest = TimeSpan.FromMilliseconds(500);

    private long _usedTicks;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyPatternBudget"/> class.
    /// </summary>
    /// <param name="total">The time the rules may take together.</param>
    public SurveyPatternBudget(TimeSpan total)
    {
        Total = total;
    }

    /// <summary>
    /// Gets the time the rules may take together.
    /// </summary>
    public TimeSpan Total { get; }

    /// <summary>
    /// Gets the time the rules took so far.
    /// </summary>
    public TimeSpan Used => TimeSpan.FromTicks(Interlocked.Read(ref _usedTicks));

    /// <summary>
    /// Gets the time left, never below zero.
    /// </summary>
    public TimeSpan Remaining => Total > Used ? Total - Used : TimeSpan.Zero;

    /// <summary>
    /// Counts the time a rule took since a timestamp from <see cref="Stopwatch.GetTimestamp()"/>.
    /// </summary>
    /// <param name="startedTimestamp">When the rule started.</param>
    public void SpendSince(long startedTimestamp)
        => Interlocked.Add(ref _usedTicks, Stopwatch.GetElapsedTime(startedTimestamp).Ticks);

    /// <summary>
    /// Counts a time the rules took.
    /// </summary>
    /// <param name="elapsed">The time.</param>
    public void Spend(TimeSpan elapsed)
        => Interlocked.Add(ref _usedTicks, Math.Max(elapsed.Ticks, 0));
}
