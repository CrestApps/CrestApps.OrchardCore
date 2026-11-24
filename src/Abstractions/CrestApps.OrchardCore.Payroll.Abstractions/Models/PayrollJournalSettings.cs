namespace CrestApps.OrchardCore.Payroll.Models;

/// <summary>
/// How statement lines become journal entries: the ledger account of each side, by default and for a pay code or a tax.
/// Payroll keeps no ledger: the entries are a data set an accounting system's file or API is written from.
/// </summary>
public sealed class PayrollJournalSettings
{
    /// <summary>Gets or sets the account net pay is owed on until it is paid (credited by pay, debited by deductions and the employee's taxes).</summary>
    public string NetPayAccount { get; set; }

    /// <summary>Gets or sets the expense account of earnings.</summary>
    public string WageExpenseAccount { get; set; }

    /// <summary>Gets or sets the liability account of deductions withheld.</summary>
    public string DeductionLiabilityAccount { get; set; }

    /// <summary>Gets or sets the liability account of taxes owed (withheld and the employer's).</summary>
    public string TaxLiabilityAccount { get; set; }

    /// <summary>Gets or sets the expense account of the employer's taxes.</summary>
    public string EmployerTaxExpenseAccount { get; set; }

    /// <summary>Gets or sets the expense account of employer contributions.</summary>
    public string ContributionExpenseAccount { get; set; }

    /// <summary>Gets or sets the liability account of employer contributions.</summary>
    public string ContributionLiabilityAccount { get; set; }

    /// <summary>Gets or sets the expense account of reimbursements.</summary>
    public string ReimbursementExpenseAccount { get; set; }

    /// <summary>Gets or sets the accounts of a pay code or a tax instead of the defaults.</summary>
    public IList<PayrollJournalMapping> Mappings { get; set; } = [];
}

/// <summary>
/// The accounts of a pay code (by its code) or a tax (by its tax code), each empty for the default.
/// </summary>
public sealed class PayrollJournalMapping
{
    /// <summary>Gets or sets the pay code's code, such as REG, or the tax code, such as US-FIT.</summary>
    public string Key { get; set; }

    /// <summary>Gets or sets the account debited, or <see langword="null"/> for the default.</summary>
    public string DebitAccount { get; set; }

    /// <summary>Gets or sets the account credited, or <see langword="null"/> for the default.</summary>
    public string CreditAccount { get; set; }
}

/// <summary>
/// A journal entry of a statement line: one account debited or credited.
/// </summary>
/// <param name="StatementId">The statement.</param>
/// <param name="StatementNumber">Its number.</param>
/// <param name="RunId">Its pay run.</param>
/// <param name="EmployerId">The employer.</param>
/// <param name="PayDate">The pay date, the entry's date.</param>
/// <param name="Account">The ledger account.</param>
/// <param name="Debit">The debit, or zero.</param>
/// <param name="Credit">The credit, or zero.</param>
/// <param name="Department">The ledger code of the line's department team, or <see langword="null"/>.</param>
/// <param name="Source">The pay code or tax code the line is of.</param>
/// <param name="Description">What the line is.</param>
/// <param name="Currency">The currency.</param>
public sealed record PayrollJournalEntry(
    string StatementId,
    string StatementNumber,
    string RunId,
    string EmployerId,
    DateOnly PayDate,
    string Account,
    decimal Debit,
    decimal Credit,
    string Department,
    string Source,
    string Description,
    string Currency);
