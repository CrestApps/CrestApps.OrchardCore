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
    /// Adds a new run (from <see cref="IPayRunManager.NewAsync"/>, with its employer, schedule or people, type and period
    /// set) and tells the observers it was opened.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="user">Who opens it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<PayRunResult> OpenAsync(PayRun run, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes an approved run: its statements become final and numbered, pay items move toward their goals and keep
    /// their arrears, the sources are told their inputs were paid, and each payment of net pay is paid by the feature of
    /// its method (a deposit with no way to pay falls back to a check). A partly paid run is processed again to pay what
    /// is left.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="user">Who processes.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<PayRunResult> ProcessAsync(PayRun run, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Voids a final statement: a reversing statement with the opposite lines cancels it in the accumulators, its pay
    /// items go back, its inputs are released, and its payments are stopped where they have not gone out.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="user">Who voids.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<PayRunResult> VoidStatementAsync(PayStatement statement, ClaimsPrincipal user, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pays a payment of a final statement again, such as a lost check: the old one is stopped and a new one made.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="index">The payment's place.</param>
    /// <param name="user">Who reissues.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<PayRunResult> ReissuePaymentAsync(PayStatement statement, int index, ClaimsPrincipal user, string reason, CancellationToken cancellationToken = default);

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
    /// <summary>
    /// Gets or sets the statement the work made, such as the reversing statement of a void.
    /// </summary>
    public PayStatement Statement { get; set; }

    /// <summary>Gets the errors; none when the move succeeded.</summary>
    public IList<string> Errors { get; } = [];

    /// <summary>Gets a value indicating whether the move succeeded.</summary>
    public bool Succeeded
        => Errors.Count == 0;
}
