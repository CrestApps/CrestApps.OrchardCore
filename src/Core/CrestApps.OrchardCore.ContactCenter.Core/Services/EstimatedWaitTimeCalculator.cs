using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Estimates how long a caller will wait: average handle time times their position, divided by the agents
/// actually working. It returns null rather than a number whenever the inputs cannot support one, because an
/// invented estimate is announced to the caller with exactly the same confidence as a real one.
/// </summary>
public static class EstimatedWaitTimeCalculator
{
    /// <summary>
    /// Estimates the wait for a caller at the given position.
    /// </summary>
    /// <param name="position">The caller's one-based position in line.</param>
    /// <param name="availableAgents">How many agents are working the queue.</param>
    /// <param name="averageHandleTime">The queue's average handle time.</param>
    /// <param name="options">The treatment options carrying the floor and the cap.</param>
    /// <returns>The estimate, or <see langword="null"/> when there is no honest one to give.</returns>
    public static TimeSpan? Estimate(int position, int availableAgents, TimeSpan averageHandleTime, QueueTreatmentSettings options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // No agents means the queue is not moving, and no history means there is nothing to extrapolate from.
        // Either way the caller is better served by an announcement that quotes no time at all.
        if (position < 1 || availableAgents < 1 || averageHandleTime <= TimeSpan.Zero)
        {
            return null;
        }

        var seconds = averageHandleTime.TotalSeconds * position / availableAgents;

        var floor = Math.Max(0, options.MinimumEstimateSeconds);
        var cap = options.MaximumEstimateSeconds > 0 ? options.MaximumEstimateSeconds : double.MaxValue;

        return TimeSpan.FromSeconds(Math.Clamp(seconds, floor, cap));
    }
}
