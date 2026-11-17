namespace CrestApps.OrchardCore.Banking.Disbursements.Models;

/// <summary>
/// What importing a return or change file did.
/// </summary>
public sealed class DisbursementImportResult
{
    /// <summary>
    /// Gets or sets how many disbursements were marked returned.
    /// </summary>
    public int Returned { get; set; }

    /// <summary>
    /// Gets or sets how many bank accounts were corrected.
    /// </summary>
    public int Corrected { get; set; }

    /// <summary>
    /// Gets or sets the lines that matched nothing or could not be applied, and why.
    /// </summary>
    public List<string> Problems { get; set; } = [];
}
