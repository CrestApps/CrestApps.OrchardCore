namespace CrestApps.OrchardCore.Checks.Models;

/// <summary>
/// The paper a layout prints on.
/// </summary>
public enum CheckStock
{
    /// <summary>
    /// Blank check paper: the layout prints the payer, the bank and the check line too.
    /// </summary>
    Blank,

    /// <summary>
    /// Paper with the payer, the bank, the numbers and the check line already printed: the layout prints only the fields.
    /// </summary>
    PrePrinted,
}
