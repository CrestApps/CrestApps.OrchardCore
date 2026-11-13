namespace CrestApps.OrchardCore.Checks.Models;

/// <summary>
/// One row of a voucher.
/// </summary>
public sealed class CheckVoucherLine
{
    /// <summary>
    /// Gets or sets the row's label, such as "Regular pay" or "Federal income tax".
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the row's values, already formatted, one per value column.
    /// </summary>
    public IReadOnlyList<string> Values { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the row is a heading or a total, printed in bold.
    /// </summary>
    public bool IsEmphasized { get; set; }
}
