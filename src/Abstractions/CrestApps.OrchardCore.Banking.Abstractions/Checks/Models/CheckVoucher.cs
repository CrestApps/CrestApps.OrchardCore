namespace CrestApps.OrchardCore.Banking.Checks.Models;

/// <summary>
/// The stub printed with a check: what the check pays, as the module that issued it describes it.
/// </summary>
public sealed class CheckVoucher
{
    /// <summary>
    /// Gets or sets the voucher's title, such as "Pay statement 1042, period 11/01 to 11/15".
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the column headings: the label, then up to three value columns (such as Hours, Current, Year to date).
    /// </summary>
    public IReadOnlyList<string> Columns { get; set; } = [];

    /// <summary>
    /// Gets or sets the rows, in order.
    /// </summary>
    public IReadOnlyList<CheckVoucherLine> Lines { get; set; } = [];
}
