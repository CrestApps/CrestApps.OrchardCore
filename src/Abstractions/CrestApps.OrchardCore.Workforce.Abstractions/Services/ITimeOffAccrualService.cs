namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Adds what policies accrue to workers' ledgers. A run writes each period's entry once, however often it runs.
/// </summary>
public interface ITimeOffAccrualService
{
    /// <summary>
    /// Accrues every worker's policies for the periods that ended on or before a day.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What the run did.</returns>
    Task<TimeOffAccrualRun> RunAsync(DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accrues one worker's policies for the periods that ended on or before a day.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="date">The day.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many ledger entries were written.</returns>
    Task<int> RunForWorkerAsync(string workerId, DateOnly date, CancellationToken cancellationToken = default);
}
