namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// What one over-dial pacing cycle of a campaign queue did.
/// </summary>
public sealed class PredictivePacingCycleResult
{
    /// <summary>
    /// The result of a cycle that did not run: another cycle of the queue held the pacing lock, or failed.
    /// </summary>
    public static PredictivePacingCycleResult NotRun { get; } = new();

    /// <summary>
    /// Gets the decision the cycle made, or <see langword="null"/> when it did not run.
    /// </summary>
    public PredictivePacingDecision Decision { get; init; }

    /// <summary>
    /// Gets the calls the cycle placed: without an agent, plus any retry of an abandoned call placed with a reserved
    /// agent.
    /// </summary>
    public int Dialed { get; init; }

    /// <summary>
    /// Gets a value indicating whether the decision is to dial the queue the reserve-then-dial way this cycle.
    /// </summary>
    public bool UseReservedFallback => Decision?.Mode == PredictivePacingDecisionMode.ReservedFallback;
}
