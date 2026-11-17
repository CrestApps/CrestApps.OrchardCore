using CrestApps.OrchardCore.Payroll.Models;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// Hears what happens to a pay run: workflows, webhooks and the audit trail are told through it. An observer runs after
/// the change is saved in the session, so it may read the run but must not save a copy of it read before.
/// </summary>
public interface IPayRunObserver
{
    /// <summary>
    /// Called once a pay run changed.
    /// </summary>
    /// <param name="change">What changed.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ChangedAsync(PayRunChange change, CancellationToken cancellationToken = default);
}

/// <summary>
/// A change of a pay run.
/// </summary>
public sealed class PayRunChange
{
    /// <summary>Gets or sets the run as it is after the change.</summary>
    public PayRun Run { get; set; }

    /// <summary>Gets or sets what happened.</summary>
    public PayRunChangeKind Kind { get; set; }

    /// <summary>Gets or sets the user name of who made the change, or <see langword="null"/> for the system.</summary>
    public string Actor { get; set; }

    /// <summary>Gets or sets the user id of who made the change, or <see langword="null"/>.</summary>
    public string ActorId { get; set; }

    /// <summary>Gets or sets a note, such as the reason of a rejection or a cancellation.</summary>
    public string Note { get; set; }
}

/// <summary>
/// What happened to a pay run.
/// </summary>
public enum PayRunChangeKind
{
    /// <summary>The run was added.</summary>
    Opened,

    /// <summary>The run was calculated.</summary>
    Calculated,

    /// <summary>The run was submitted for approval.</summary>
    Submitted,

    /// <summary>The run was approved.</summary>
    Approved,

    /// <summary>The run's approval was rejected.</summary>
    Rejected,

    /// <summary>The run was processed and every statement paid.</summary>
    Paid,

    /// <summary>The run was processed and some payments wait.</summary>
    PartiallyPaid,

    /// <summary>The run was processed and nothing could be paid.</summary>
    Failed,

    /// <summary>The run was cancelled.</summary>
    Canceled,
}
