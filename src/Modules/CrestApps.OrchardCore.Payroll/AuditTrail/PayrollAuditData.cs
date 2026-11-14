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
}
