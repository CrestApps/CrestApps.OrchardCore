using CrestApps.OrchardCore.Payroll.Models;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// Hears what happens to tax filings and payroll years: workflows are told through it. An observer runs after the change
/// is saved in the session, before it is committed.
/// </summary>
public interface IPayrollFilingObserver
{
    /// <summary>
    /// Called once a filing or a payroll year changed.
    /// </summary>
    /// <param name="change">What changed.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ChangedAsync(PayrollFilingChange change, CancellationToken cancellationToken = default);
}

/// <summary>
/// A change of a tax filing or of an employer's payroll year.
/// </summary>
public sealed class PayrollFilingChange
{
    /// <summary>Gets or sets what happened.</summary>
    public PayrollFilingChangeKind Kind { get; set; }

    /// <summary>Gets or sets the employer.</summary>
    public string EmployerId { get; set; }

    /// <summary>Gets or sets the tax year.</summary>
    public int Year { get; set; }

    /// <summary>Gets or sets the filing as it is after the change, or <see langword="null"/> for a change of the year.</summary>
    public PayrollFiling Filing { get; set; }

    /// <summary>Gets or sets the user name of who made the change, or <see langword="null"/> for the system.</summary>
    public string Actor { get; set; }

    /// <summary>Gets or sets the user id of who made the change, or <see langword="null"/>.</summary>
    public string ActorId { get; set; }
}

/// <summary>
/// What happened to a tax filing or a payroll year.
/// </summary>
public enum PayrollFilingChangeKind
{
    /// <summary>A draft filing was prepared, or prepared again.</summary>
    Prepared,

    /// <summary>A draft filing was marked as filed.</summary>
    Filed,

    /// <summary>An amendment of a filed filing was started; the filing is the new draft.</summary>
    Amended,

    /// <summary>The employer's payroll year was closed.</summary>
    YearClosed,

    /// <summary>The employer's payroll year was reopened.</summary>
    YearReopened,
}
