using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// A tax form, made on the Tax Forms page or by a recipe: its fields (the form's boxes and lines) and how each is worked
/// out, its yearly values and checks, and how a recipient's statement is printed. The module named by
/// <see cref="Source"/> prepares it from its own records (Payroll from pay statements) and keeps what it files; it can
/// keep settings of its own on the form, such as whose pay a payroll form reports.
/// </summary>
public sealed class TaxForm : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>Gets or sets the stored name filings keep, such as US-941. It never changes once a form is filed.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets what people see, such as Form 941 (quarterly federal return).</summary>
    public string DisplayName { get; set; }

    /// <summary>Gets or sets what the form is for.</summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the tax form source that prepares the form, such as Payroll: it supplies the functions the formulas
    /// read its records with (<c>AddTaxFormSource</c>).
    /// </summary>
    public string Source { get; set; }

    /// <summary>Gets or sets the jurisdiction a filer must be registered in to file the form, such as US or CA-QC.</summary>
    public string Jurisdiction { get; set; }

    /// <summary>Gets or sets the period a filing covers.</summary>
    public TaxFormPeriod Period { get; set; }

    /// <summary>
    /// Gets or sets whether each recipient gets their own statement of the form (such as a W-2 or a 1099): kept on the
    /// filing, printed and sent to them. Without it, recipient fields are worked out only to be added up on the filing.
    /// </summary>
    public bool HasRecipientStatements { get; set; }

    /// <summary>
    /// Gets or sets whether a recipient gets a statement for each jurisdiction under <see cref="Jurisdiction"/> their
    /// figures fall in, such as a T4 slip per province of employment.
    /// </summary>
    public bool StatementPerJurisdiction { get; set; }

    /// <summary>
    /// Gets or sets the formula that says whether a recipient is reported, such as <c>[1] &gt;= VALUE('Threshold') OR [4] &gt; 0</c>.
    /// Empty reports a recipient with any recipient field that is not zero.
    /// </summary>
    public string RecipientCondition { get; set; }

    /// <summary>
    /// Gets or sets the note added for a reported recipient with no tax id on file, with {0} their name, such as
    /// "{0}: no tax id on file.". Empty adds none.
    /// </summary>
    public string MissingTaxIdNote { get; set; }

    /// <summary>Gets or sets the yearly values the formulas read with VALUE, such as a reporting threshold.</summary>
    public IList<TaxFormValue> Values { get; set; } = [];

    /// <summary>Gets or sets the checks that add a note to a filing when they fail, such as a missing account number.</summary>
    public IList<TaxFormCheck> Checks { get; set; } = [];

    /// <summary>Gets or sets the fields (the boxes and lines of the form), in the order they are shown.</summary>
    public IList<TaxFormField> Fields { get; set; } = [];

    /// <summary>Gets or sets how a recipient's statement is printed.</summary>
    public TaxFormStatement Statement { get; set; } = new();

    /// <summary>Gets or sets whether filers can prepare the form.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets when it was added.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <inheritdoc />
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>Gets whether the form is for a quarter (otherwise a year).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsQuarterly
        => Period == TaxFormPeriod.Quarter;
}

/// <summary>
/// The period a filing of a tax form covers.
/// </summary>
public enum TaxFormPeriod
{
    /// <summary>A calendar year.</summary>
    Year,

    /// <summary>A calendar quarter.</summary>
    Quarter,
}

/// <summary>
/// Where a field of a tax form is.
/// </summary>
public enum TaxFormFieldLevel
{
    /// <summary>On each recipient's statement, worked out from the recipient's figures.</summary>
    Recipient,

    /// <summary>On the filing, worked out from every reported recipient's figures.</summary>
    Filing,
}

/// <summary>
/// When a recipient field is printed on the recipient's statement.
/// </summary>
public enum TaxFormFieldPrint
{
    /// <summary>Never.</summary>
    Never,

    /// <summary>Always, zero or not.</summary>
    Always,

    /// <summary>Only when it is not zero.</summary>
    WhenNotZero,
}

/// <summary>
/// A field of a tax form: one of its boxes (W-2 box 1) or lines (Form 941 line 5a).
/// </summary>
public sealed class TaxFormField
{
    /// <summary>Gets or sets the box or line number as filings keep it, such as 5a or 12D. Formulas read it as [5a].</summary>
    public string Number { get; set; }

    /// <summary>Gets or sets the label, such as "5a Taxable social security wages".</summary>
    public string Label { get; set; }

    /// <summary>Gets or sets where the field is.</summary>
    public TaxFormFieldLevel Level { get; set; }

    /// <summary>
    /// Gets or sets the formula that works out the field, such as <c>GREATEST(0, TAXABLE('US-SS-EE') - [5b])</c>. Empty is
    /// zero, plus what the source's records report to the field when <see cref="AcceptsReportedAmounts"/> is on.
    /// </summary>
    public string Formula { get; set; }

    /// <summary>Gets or sets whether a recipient field is also added up on the filing, under the same number.</summary>
    public bool AddToFiling { get; set; }

    /// <summary>
    /// Gets or sets whether the source's records can report amounts to the field, such as pay codes for Payroll:
    /// they are added to what the formula gives.
    /// </summary>
    public bool AcceptsReportedAmounts { get; set; }

    /// <summary>Gets or sets when a recipient field is printed on the recipient's statement.</summary>
    public TaxFormFieldPrint Print { get; set; }
}

/// <summary>
/// A yearly value of a tax form, such as a reporting threshold or a maximum: the value of the latest year at or before
/// the filing's year applies.
/// </summary>
public sealed class TaxFormValue
{
    /// <summary>Gets or sets the name formulas read it by, such as Threshold.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the first year it applies to.</summary>
    public int Year { get; set; }

    /// <summary>Gets or sets the value.</summary>
    public decimal Amount { get; set; }
}

/// <summary>
/// A check of a tax form: a formula that must be true for a filing, or a note is added to it.
/// </summary>
public sealed class TaxFormCheck
{
    /// <summary>Gets or sets the formula that must be true, such as <c>ISACCOUNT(ACCOUNT('CA'), '#########RP####')</c>.</summary>
    public string Condition { get; set; }

    /// <summary>Gets or sets the note added when it is not.</summary>
    public string Note { get; set; }
}

/// <summary>
/// How a recipient's statement of a tax form is printed and named.
/// </summary>
public sealed class TaxFormStatement
{
    /// <summary>Gets or sets what a recipient's statement is called, such as Form W-2 or T4 slip.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the title printed at the top, such as Form W-2 Wage and Tax Statement.</summary>
    public string Title { get; set; }

    /// <summary>Gets or sets the line under the title, such as which copy it is.</summary>
    public string Copy { get; set; }

    /// <summary>Gets or sets the label of the recipient's tax id.</summary>
    public string TaxIdLabel { get; set; }

    /// <summary>Gets or sets how the recipient's masked tax id is printed, with {0} its last characters, such as XXX-XX-{0}.</summary>
    public string TaxIdFormat { get; set; }

    /// <summary>Gets or sets the label of the filer's number.</summary>
    public string FilerTaxIdLabel { get; set; }

    /// <summary>
    /// Gets or sets the jurisdiction of the filer's registration whose account number is printed, such as CA. Empty
    /// prints the filer's tax id.
    /// </summary>
    public string FilerAccountJurisdiction { get; set; }

    /// <summary>Gets or sets the start of the file name, such as W-2 for W-2-2026.pdf. Empty uses the form's name.</summary>
    public string FileName { get; set; }
}
