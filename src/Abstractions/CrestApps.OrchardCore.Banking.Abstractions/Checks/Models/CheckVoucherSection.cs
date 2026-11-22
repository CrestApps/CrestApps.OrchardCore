namespace CrestApps.OrchardCore.Banking.Checks.Models;

/// <summary>
/// One part of a voucher with its own table, such as the earnings, the taxes or the deductions of a pay stub.
/// </summary>
public sealed class CheckVoucherSection
{
    /// <summary>
    /// Gets or sets the section's title, such as "Earnings"; empty for none.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the column headings: the label, then the value columns (such as Hours, Rate, Current, Year to date).
    /// Up to six value columns fit; the label column narrows as they are added.
    /// </summary>
    public IReadOnlyList<string> Columns { get; set; } = [];

    /// <summary>
    /// Gets or sets the rows, in order. A row marked <see cref="CheckVoucherLine.IsEmphasized"/> prints in bold, such as
    /// the section's total.
    /// </summary>
    public IReadOnlyList<CheckVoucherLine> Lines { get; set; } = [];
}
