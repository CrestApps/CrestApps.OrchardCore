using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Predicts how many busy agents will be free to take a call within a horizon, typically the time a call placed now takes
/// to be answered, so over-dialing can count them alongside the agents free now.
/// </summary>
/// <remarks>
/// <para>
/// An agent is expected to finish their current phase when its average length has passed. A talking agent then wraps up
/// too, so their time to free is the rest of the average call plus a whole average wrap-up; a wrapping agent's is the rest
/// of the average wrap-up. The agent is counted when that time fits in the horizon.
/// </para>
/// <para>
/// The prediction errs toward fewer agents, because counting an agent who does not free up abandons the call they were
/// counted for. An agent already past the average gets no credit: a call or wrap-up that has run long says nothing
/// reliable about when it will end. Without a measured average for a phase, nobody in that phase is counted.
/// </para>
/// </remarks>
public static class AgentFreeUpPredictor
{
    /// <summary>
    /// Counts the busy agents expected to be free within the horizon.
    /// </summary>
    /// <param name="agents">What each busy agent is doing now.</param>
    /// <param name="averageTalkTime">The measured average time on a call, or <see langword="null"/> when unknown.</param>
    /// <param name="averageWrapUpTime">The measured average wrap-up, or <see langword="null"/> when unknown.</param>
    /// <param name="horizon">How far ahead an agent may free up and still be counted.</param>
    /// <returns>The number of agents expected to be free within the horizon.</returns>
    public static int CountFreeingWithin(
        IEnumerable<AgentWorkSnapshot> agents,
        TimeSpan? averageTalkTime,
        TimeSpan? averageWrapUpTime,
        TimeSpan horizon)
    {
        ArgumentNullException.ThrowIfNull(agents);

        if (horizon <= TimeSpan.Zero)
        {
            return 0;
        }

        var count = 0;

        foreach (var agent in agents)
        {
            if (agent is not null &&
                PredictTimeToFree(agent, averageTalkTime, averageWrapUpTime) is { } timeToFree &&
                timeToFree <= horizon)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Predicts how long until one busy agent is free.
    /// </summary>
    /// <param name="agent">What the agent is doing now.</param>
    /// <param name="averageTalkTime">The measured average time on a call, or <see langword="null"/> when unknown.</param>
    /// <param name="averageWrapUpTime">The measured average wrap-up, or <see langword="null"/> when unknown.</param>
    /// <returns>
    /// The predicted time until the agent is free, or <see langword="null"/> when no prediction can be made: an average is
    /// unknown, or the agent is already past it.
    /// </returns>
    public static TimeSpan? PredictTimeToFree(AgentWorkSnapshot agent, TimeSpan? averageTalkTime, TimeSpan? averageWrapUpTime)
    {
        ArgumentNullException.ThrowIfNull(agent);

        // Wrap-up follows every call, so no phase can be predicted without its average.
        if (averageWrapUpTime is not { } wrapUp || wrapUp < TimeSpan.Zero)
        {
            return null;
        }

        var elapsed = agent.Elapsed < TimeSpan.Zero ? TimeSpan.Zero : agent.Elapsed;

        switch (agent.Phase)
        {
            case AgentWorkPhase.WrappingUp:
                return elapsed < wrapUp ? wrapUp - elapsed : null;

            case AgentWorkPhase.Talking:
                if (averageTalkTime is not { } talk || elapsed >= talk)
                {
                    return null;
                }

                return talk - elapsed + wrapUp;

            default:
                return null;
        }
    }
}
