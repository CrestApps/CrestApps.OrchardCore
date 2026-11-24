using System.Text.Json.Serialization;
using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Payroll.Models;

/// <summary>
/// A tax form an employer files, made on the Tax Forms page or by a recipe: who it reports, its fields and how each field
/// is worked out from the period's final pay statements. Prepared into a <see cref="PayrollFiling"/> whose
/// <see cref="PayrollFiling.Form"/> is the form's <see cref="Name"/>.
/// </summary>
public sealed class PayrollTaxForm : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>Gets or sets the stored name filings keep, such as US-941. It never changes once a form is filed.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets what people see, such as Form 941 (quarterly federal return).</summary>
    public string DisplayName { get; set; }

    /// <summary>Gets or sets what the form is for.</summary>
    public string Description { get; set; }

    /// <summary>Gets or sets the jurisdiction an employer must be registered in to file the form, such as US or CA-QC.</summary>
    public string Jurisdiction { get; set; }

    /// <summary>Gets or sets the period a filing covers.</summary>
    public PayrollTaxFormPeriod Period { get; set; }

    /// <summary>Gets or sets whose pay the form reports.</summary>
    public PayrollTaxFormWorkers Workers { get; set; }

    /// <summary>
    /// Gets or sets the jurisdiction a person must have a tax line of to be reported, such as CA-QC: a person with a tax
    /// line of that jurisdiction or one under it counts. Empty reports everyone of <see cref="Workers"/>.
    /// </summary>
    public string PeopleJurisdiction { get; set; }

    /// <summary>
    /// Gets or sets whether each person gets their own statement of the form (such as a wage statement): stored on the
    /// filing, printed and sent to them. Without it, person fields are worked out only to be added up on the filing.
    /// </summary>
    public bool HasPersonStatements { get; set; }

    /// <summary>
    /// Gets or sets whether a person gets a statement for each jurisdiction under <see cref="Jurisdiction"/> they paid
    /// tax in, such as a T4 slip per province of employment.
    /// </summary>
    public bool StatementPerJurisdiction { get; set; }

    /// <summary>
    /// Gets or sets whether each person's statement lists the wages and income tax of every jurisdiction below the
    /// country they paid income tax in, with the employer's account there, such as a W-2's state and local boxes 15 to 20.
    /// </summary>
    public bool ListJurisdictionAmounts { get; set; }

    /// <summary>
    /// Gets or sets the formula that says whether a person is reported, such as <c>[1] &gt;= VALUE('Threshold') OR [4] &gt; 0</c>.
    /// Empty reports a person with any person field that is not zero.
    /// </summary>
    public string PersonCondition { get; set; }

    /// <summary>
    /// Gets or sets the note added for a reported person with no tax id on file, with {0} their name, such as
    /// "{0}: no tax id on file.". Empty adds none.
    /// </summary>
    public string MissingTaxIdNote { get; set; }

    /// <summary>
    /// Gets or sets the prefix of the names pay codes report to a field by, such as W2: for W2:12D. Empty uses the form's
    /// name and a colon.
    /// </summary>
    public string PayCodeFieldPrefix { get; set; }

    /// <summary>Gets or sets the yearly values the formulas read with VALUE, such as a reporting threshold.</summary>
    public IList<PayrollTaxFormValue> Values { get; set; } = [];

    /// <summary>Gets or sets the checks that add a note to a filing when they fail, such as a missing account number.</summary>
    public IList<PayrollTaxFormCheck> Checks { get; set; } = [];

    /// <summary>Gets or sets the fields (the boxes and lines of the form), in the order they are shown.</summary>
    public IList<PayrollTaxFormField> Fields { get; set; } = [];

    /// <summary>Gets or sets how a person's statement is printed.</summary>
    public PayrollTaxFormStatement Statement { get; set; } = new();

    /// <summary>Gets or sets whether employers can prepare the form.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets when it was added.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <inheritdoc />
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>Gets whether the form is for a quarter (otherwise a year).</summary>
    [JsonIgnore]
    public bool IsQuarterly
        => Period == PayrollTaxFormPeriod.Quarter;

    /// <summary>Gets the prefix of the names pay codes report to a field by.</summary>
    [JsonIgnore]
    public string EffectivePayCodeFieldPrefix
        => string.IsNullOrWhiteSpace(PayCodeFieldPrefix) ? Name + ":" : PayCodeFieldPrefix.Trim();
}

/// <summary>
/// The period a filing of a tax form covers.
/// </summary>
public enum PayrollTaxFormPeriod
{
    /// <summary>A calendar year.</summary>
    Year,

    /// <summary>A calendar quarter.</summary>
    Quarter,
}

/// <summary>
/// Whose pay a tax form reports.
/// </summary>
public enum PayrollTaxFormWorkers
{
    /// <summary>Employees only.</summary>
    Employees,

    /// <summary>Contractors only.</summary>
    Contractors,

    /// <summary>Employees and contractors.</summary>
    Everyone,
}

/// <summary>
/// Where a field of a tax form is.
/// </summary>
public enum PayrollTaxFormFieldLevel
{
    /// <summary>On each person's statement, worked out from the person's statements.</summary>
    Person,

    /// <summary>On the filing, worked out from every reported person's statements.</summary>
    Filing,
}

/// <summary>
/// When a person field is printed on the person's statement.
/// </summary>
public enum PayrollTaxFormFieldPrint
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
public sealed class PayrollTaxFormField
{
    /// <summary>Gets or sets the box or line number as filings keep it, such as 5a or 12D. Formulas read it as [5a].</summary>
    public string Number { get; set; }

    /// <summary>Gets or sets the label, such as "5a Taxable social security wages".</summary>
    public string Label { get; set; }

    /// <summary>Gets or sets where the field is.</summary>
    public PayrollTaxFormFieldLevel Level { get; set; }

    /// <summary>
    /// Gets or sets the formula that works out the field, such as <c>GREATEST(0, TAXABLE('US-SS-EE') - [5b])</c>. Empty is
    /// zero, plus what pay codes report to the field when <see cref="PayCodes"/> is on.
    /// </summary>
    public string Formula { get; set; }

    /// <summary>Gets or sets whether a person field is also added up on the filing, under the same number.</summary>
    public bool AddToFiling { get; set; }

    /// <summary>
    /// Gets or sets whether pay codes can report their amounts to the field (Pay Codes, Tax form fields): they are added to what
    /// the formula gives.
    /// </summary>
    public bool PayCodes { get; set; }

    /// <summary>Gets or sets when a person field is printed on the person's statement.</summary>
    public PayrollTaxFormFieldPrint Print { get; set; }
}

/// <summary>
/// A yearly value of a tax form, such as a reporting threshold or a maximum: the value of the latest year at or before
/// the filing's year applies.
/// </summary>
public sealed class PayrollTaxFormValue
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
public sealed class PayrollTaxFormCheck
{
    /// <summary>Gets or sets the formula that must be true, such as <c>ISACCOUNT(ACCOUNT('CA'), '#########RP####')</c>.</summary>
    public string Condition { get; set; }

    /// <summary>Gets or sets the note added when it is not.</summary>
    public string Note { get; set; }
}

/// <summary>
/// How a person's statement of a tax form is printed and named.
/// </summary>
public sealed class PayrollTaxFormStatement
{
    /// <summary>Gets or sets what a person's statement is called, such as Form W-2 or T4 slip.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the title printed at the top, such as Form W-2 Wage and Tax Statement.</summary>
    public string Title { get; set; }

    /// <summary>Gets or sets the line under the title, such as which copy it is.</summary>
    public string Copy { get; set; }

    /// <summary>Gets or sets the label of the person's tax id.</summary>
    public string TaxIdLabel { get; set; }

    /// <summary>Gets or sets how the person's masked tax id is printed, with {0} its last characters, such as XXX-XX-{0}.</summary>
    public string TaxIdFormat { get; set; }

    /// <summary>Gets or sets the label of the employer's number.</summary>
    public string EmployerTaxIdLabel { get; set; }

    /// <summary>
    /// Gets or sets the jurisdiction of the employer's registration whose account number is printed, such as CA. Empty
    /// prints the employer's tax id.
    /// </summary>
    public string EmployerAccountJurisdiction { get; set; }

    /// <summary>Gets or sets the start of the file name, such as W-2 for W-2-2026.pdf. Empty uses the form's name.</summary>
    public string FileName { get; set; }
}
