namespace CrestApps.OrchardCore.Banking.Checks.Models;

/// <summary>
/// The stub printed with a check: what the check pays, as the module that issued it describes it. A simple voucher is
/// one table (<see cref="Columns"/> and <see cref="Lines"/>); a pay stub is several <see cref="Sections"/>, each with
/// its own title and columns, such as earnings, taxes, deductions and totals.
/// </summary>
public sealed class CheckVoucher
{
    /// <summary>
    /// Gets or sets the voucher's title, such as "Pay statement 1042, period 11/01 to 11/15".
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the column headings of a voucher of one table: the label, then the value columns (such as Hours,
    /// Current, Year to date). Up to six value columns fit.
    /// </summary>
    public IReadOnlyList<string> Columns { get; set; } = [];

    /// <summary>
    /// Gets or sets the rows of a voucher of one table, in order.
    /// </summary>
    public IReadOnlyList<CheckVoucherLine> Lines { get; set; } = [];

    /// <summary>
    /// Gets or sets the sections, in order, each with its own title, columns and rows. When the voucher has
    /// <see cref="Columns"/> or <see cref="Lines"/> too, that table prints first, as a section without a title.
    /// </summary>
    public IReadOnlyList<CheckVoucherSection> Sections { get; set; } = [];

    /// <summary>
    /// Returns the tables to print: the voucher's own table, when it has one, then its sections.
    /// </summary>
    /// <returns>The sections, in order.</returns>
    public IReadOnlyList<CheckVoucherSection> GetSections()
    {
        var sections = new List<CheckVoucherSection>();

        if (Columns is { Count: > 0 } || Lines is { Count: > 0 })
        {
            sections.Add(new CheckVoucherSection { Columns = Columns ?? [], Lines = Lines ?? [] });
        }

        if (Sections is not null)
        {
            sections.AddRange(Sections.Where(section => section is not null));
        }

        return sections;
    }
}
