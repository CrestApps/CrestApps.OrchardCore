using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Decides how a pacing cycle that ran to the end ended, from what it placed and what it was seen to decide.
/// </summary>
internal static class DialerPacingCycleClassifier
{
    /// <summary>
    /// The outcomes of a cycle that ran to the end and decided something. The dialer's heartbeat counts these.
    /// </summary>
    public static readonly IReadOnlyList<DialerPacingCycleOutcome> CompletedOutcomes =
    [
        DialerPacingCycleOutcome.Placed,
        DialerPacingCycleOutcome.NothingDue,
        DialerPacingCycleOutcome.NoAgents,
        DialerPacingCycleOutcome.HeldBack,
    ];

    /// <summary>
    /// Whether a cycle that ended this way ran to the end and decided something.
    /// </summary>
    /// <param name="outcome">How the cycle ended.</param>
    public static bool IsCompleted(DialerPacingCycleOutcome outcome)
        => outcome is DialerPacingCycleOutcome.Placed
            or DialerPacingCycleOutcome.NothingDue
            or DialerPacingCycleOutcome.NoAgents
            or DialerPacingCycleOutcome.HeldBack;

    /// <summary>
    /// Classifies a cycle that ran to the end.
    /// </summary>
    /// <param name="placed">How many calls the cycle placed.</param>
    /// <param name="observation">What the cycle was seen to decide, or <see langword="null"/> when nothing was watching.</param>
    /// <param name="configurationProblem">
    /// Why the profile could not be dialed at all, such as no voice provider routing outbound calls, or
    /// <see langword="null"/>.
    /// </param>
    /// <param name="policyForbidsDialing">Whether the abandonment policy does not permit the profile to dial.</param>
    /// <returns>The outcome and a short description of why, when there is one to give.</returns>
    public static (DialerPacingCycleOutcome Outcome, string Detail) Classify(
        int placed,
        DialerPacingCycleObservation observation,
        string configurationProblem,
        bool policyForbidsDialing)
    {
        if (placed > 0)
        {
            return (DialerPacingCycleOutcome.Placed, null);
        }

        // The profile cannot be dialed at all: every cycle will place nothing until someone fixes the configuration, which
        // is a failure to report rather than a quiet cycle.
        if (!string.IsNullOrEmpty(configurationProblem))
        {
            return (DialerPacingCycleOutcome.Failed, configurationProblem);
        }

        var suppressed = observation?.Suppressed ?? 0;
        var routingSucceeded = observation?.RoutingSucceeded ?? 0;
        var routingNoAgent = observation?.RoutingNoAgent ?? 0;

        if (suppressed > 0)
        {
            return (DialerPacingCycleOutcome.HeldBack, $"Suppressed by the compliance gate: {observation.DescribeSuppressions()}.");
        }

        if (routingNoAgent > 0 && routingSucceeded == 0)
        {
            return (DialerPacingCycleOutcome.NoAgents, "Routing found no available agent for the record at the head of the queue.");
        }

        if (policyForbidsDialing)
        {
            return (DialerPacingCycleOutcome.HeldBack, "The abandonment policy does not permit the profile to dial.");
        }

        if (routingSucceeded > 0)
        {
            return (DialerPacingCycleOutcome.HeldBack, "An agent was reserved but the call was not placed.");
        }

        return (DialerPacingCycleOutcome.NothingDue, null);
    }
}
