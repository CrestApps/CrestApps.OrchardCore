using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;

/// <summary>
/// One agent's time over one interval: how long they were signed in, in each state, and the work they handled.
/// </summary>
public sealed class AgentIntervalStatistic
{
    /// <summary>
    /// Gets or sets the agent profile.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the agent's user.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the UTC start of the interval.
    /// </summary>
    public DateTime IntervalStartUtc { get; set; }

    /// <summary>
    /// Gets or sets the interval's length in minutes.
    /// </summary>
    public int IntervalMinutes { get; set; } = IntervalStatisticsConstants.IntervalMinutes;

    /// <summary>
    /// Gets or sets the seconds the agent was in any state but offline.
    /// </summary>
    public double SignedInSeconds { get; set; }

    /// <summary>
    /// Gets or sets the seconds in each state and reason.
    /// </summary>
    public List<AgentIntervalState> States { get; set; } = [];

    /// <summary>
    /// Gets or sets the work handled, per queue.
    /// </summary>
    public List<AgentIntervalQueue> Queues { get; set; } = [];

    /// <summary>
    /// Gets the work handled over every queue.
    /// </summary>
    public long Handled => Queues.Sum(queue => queue.Handled);

    /// <summary>
    /// Gets the handle seconds over every queue.
    /// </summary>
    public double HandleSeconds => Queues.Sum(queue => queue.HandleSeconds);

    /// <summary>
    /// Gets the seconds in one state, whatever the reason.
    /// </summary>
    /// <param name="status">The state.</param>
    /// <returns>The seconds.</returns>
    public double SecondsIn(AgentPresenceStatus status)
        => States.Where(state => state.Status == status).Sum(state => state.Seconds);
}

/// <summary>
/// The seconds an agent spent in one state, with one reason, during an interval.
/// </summary>
public sealed class AgentIntervalState
{
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public AgentPresenceStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the reason, such as a break's reason code, or <see langword="null"/>.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the seconds.
    /// </summary>
    public double Seconds { get; set; }
}

/// <summary>
/// The work an agent handled from one queue during an interval.
/// </summary>
public sealed class AgentIntervalQueue
{
    /// <summary>
    /// Gets or sets the queue, or an empty string for work without one.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the work handled.
    /// </summary>
    public long Handled { get; set; }

    /// <summary>
    /// Gets or sets the handle seconds: talk and wrap-up.
    /// </summary>
    public double HandleSeconds { get; set; }
}
