using CrestApps.OrchardCore.Payroll.Models;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// Hears what a garnishment order withholds and sends to its payee: workflows are told through it. An observer runs after
/// the order is saved in the session, before it is committed.
/// </summary>
public interface IGarnishmentObserver
{
    /// <summary>
    /// Called once a garnishment order's withholding changed.
    /// </summary>
    /// <param name="change">What changed.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ChangedAsync(GarnishmentChange change, CancellationToken cancellationToken = default);
}

/// <summary>
/// A change of what a garnishment order withheld.
/// </summary>
public sealed class GarnishmentChange
{
    /// <summary>Gets or sets what happened.</summary>
    public GarnishmentChangeKind Kind { get; set; }

    /// <summary>Gets or sets the order as it is after the change.</summary>
    public GarnishmentOrder Order { get; set; }

    /// <summary>Gets or sets the remittance of the statement, with its payment or problem.</summary>
    public GarnishmentRemittance Remittance { get; set; }

    /// <summary>Gets or sets the pay statement it was withheld from.</summary>
    public PayStatement Statement { get; set; }
}

/// <summary>
/// What happened to a garnishment order's withholding.
/// </summary>
public enum GarnishmentChangeKind
{
    /// <summary>An amount was withheld from a statement and sent to the payee, or recorded as paid outside payroll.</summary>
    Withheld,

    /// <summary>The statement was voided: the withholding was given back and the payee's payment stopped.</summary>
    RemittanceVoided,

    /// <summary>The order owes nothing more and is completed.</summary>
    Completed,
}
