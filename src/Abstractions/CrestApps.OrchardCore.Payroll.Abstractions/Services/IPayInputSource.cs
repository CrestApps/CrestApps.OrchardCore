using CrestApps.OrchardCore.Payroll.Models;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// A module that hands pay inputs to payroll, such as Workforce's locked timesheets. Register one with
/// <c>services.AddPayInputSource&lt;T&gt;()</c>.
/// </summary>
public interface IPayInputSource
{
    /// <summary>
    /// Gets the kind of record the inputs come from, such as Timesheet. It is unique among the sources.
    /// </summary>
    string SourceKind { get; }

    /// <summary>
    /// Reads the inputs.
    /// </summary>
    /// <param name="query">What to read.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The inputs.</returns>
    Task<IReadOnlyList<PayInput>> GetInputsAsync(PayInputQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a pay run or an export took the inputs of these records, so they are not taken again.
    /// </summary>
    /// <param name="consumerId">Who took them.</param>
    /// <param name="sourceIds">The records of this source.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task MarkConsumedAsync(string consumerId, IReadOnlyCollection<string> sourceIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives back what a pay run or an export took, such as when the pay run is deleted.
    /// </summary>
    /// <param name="consumerId">Who took them.</param>
    /// <param name="sourceIds">The records of this source.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ReleaseAsync(string consumerId, IReadOnlyCollection<string> sourceIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads the pay inputs of every source, and records what was taken.
/// </summary>
public interface IPayInputService
{
    /// <summary>
    /// Reads the inputs of every source.
    /// </summary>
    /// <param name="query">What to read.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The inputs.</returns>
    Task<IReadOnlyList<PayInput>> GetInputsAsync(PayInputQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the records were taken, telling each source about its own.
    /// </summary>
    /// <param name="consumption">Who took what.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task MarkConsumedAsync(PayInputConsumption consumption, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives the records back, telling each source about its own.
    /// </summary>
    /// <param name="consumption">Who gives back what.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ReleaseAsync(PayInputConsumption consumption, CancellationToken cancellationToken = default);
}
