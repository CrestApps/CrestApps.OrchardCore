namespace CrestApps.OrchardCore.Payroll.AuditTrail;

/// <summary>
/// What the Audit Trail keeps of a pay profile change: never the tax id itself.
/// </summary>
public sealed class PayrollAuditData
{
    /// <summary>
    /// Gets or sets the staff member the profile pays.
    /// </summary>
    public string StaffMemberId { get; set; }

    /// <summary>
    /// Gets or sets the employer of the profile.
    /// </summary>
    public string EmployerId { get; set; }

    /// <summary>
    /// Gets or sets the last characters of the tax id, as the page shows them.
    /// </summary>
    public string TaxIdHint { get; set; }

    /// <summary>
    /// Gets or sets why the change or the reveal happened, such as the page it came from.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>Gets or sets the pay run's number, for a pay run event.</summary>
    public string RunNumber { get; set; }

    /// <summary>Gets or sets the statement's number, for a statement event.</summary>
    public string StatementNumber { get; set; }

    /// <summary>Gets or sets what happened, such as Approved or Voided.</summary>
    public string Change { get; set; }

    /// <summary>Gets or sets how many statements the run has.</summary>
    public int? Statements { get; set; }
}
