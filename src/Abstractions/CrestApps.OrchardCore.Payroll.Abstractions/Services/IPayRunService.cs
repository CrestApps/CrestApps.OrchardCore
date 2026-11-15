using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// Manages pay runs, so their handlers run.
/// </summary>
public interface IPayRunManager : ICatalogManager<PayRun>
{
    /// <summary>
    /// Lists the runs of some employers, newest pay date first.
    /// </summary>
    /// <param name="employerIds">The employers.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The runs.</returns>
    Task<IReadOnlyList<PayRun>> ListByEmployersAsync(IReadOnlyCollection<string> employerIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Manages the inputs entered for pay runs.
/// </summary>
public interface IPayRunInputManager : ICatalogManager<PayRunInput>
{
    /// <summary>
    /// Lists the inputs of a run.
    /// </summary>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The inputs.</returns>
    Task<IReadOnlyList<PayRunInput>> ListByRunAsync(string runId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Moves pay runs through their life: calculating their draft statements, submitting them for approval, taking the
/// decision, and cancelling them. Every move follows <see cref="PayRunStatuses.Transitions"/>.
/// </summary>
public interface IPayRunService
{
    /// <summary>
    /// Calculates the run's draft statements, for everyone or for some staff members (an input changed), and its
    /// totals and issues. A run waiting for approval or approved goes back to calculated: an approval is for one
    /// calculation.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="user">Who calculates.</param>
    /// <param name="staffMemberIds">The staff members to calculate again, or <see langword="null"/> for everyone.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<PayRunResult> CalculateAsync(PayRun run, ClaimsPrincipal user, IReadOnlyCollection<string> staffMemberIds = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a calculated run with no blocker for approval, through Approvals' pay run policy.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="user">Who submits.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<PayRunResult> SubmitAsync(PayRun run, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an open run: its approval request is cancelled and its draft statements removed.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<PayRunResult> CancelAsync(PayRun run, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the draft statements of a run (or its final ones, once processed).
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The statements.</returns>
    Task<IReadOnlyList<PayStatement>> ListStatementsAsync(PayRun run, CancellationToken cancellationToken = default);
}

/// <summary>
/// The result of a pay run move.
/// </summary>
public sealed class PayRunResult
{
    /// <summary>Gets the errors; none when the move succeeded.</summary>
    public IList<string> Errors { get; } = [];

    /// <summary>Gets a value indicating whether the move succeeded.</summary>
    public bool Succeeded
        => Errors.Count == 0;
}
