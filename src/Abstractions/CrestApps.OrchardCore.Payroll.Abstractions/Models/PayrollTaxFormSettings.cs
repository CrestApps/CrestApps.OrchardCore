using CrestApps.Core;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Payroll.Models;

/// <summary>
/// What Payroll keeps on a tax form it prepares (a form whose source is Payroll), in the form's properties, from the
/// Payroll card of the tax form editor: whose pay the form reports and how pay codes report to its fields.
/// </summary>
public sealed class PayrollTaxFormSettings
{
    /// <summary>Gets or sets whose pay the form reports.</summary>
    public PayrollTaxFormWorkers Workers { get; set; }

    /// <summary>
    /// Gets or sets the jurisdiction a person must have a tax line of to be reported, such as CA-QC: a person with a tax
    /// line of that jurisdiction or one under it counts. Empty reports everyone of <see cref="Workers"/>.
    /// </summary>
    public string PeopleJurisdiction { get; set; }

    /// <summary>
    /// Gets or sets whether each person's statement lists the wages and income tax of every jurisdiction below the
    /// country they paid income tax in, with the employer's account there, such as a W-2's state and local boxes 15 to 20.
    /// </summary>
    public bool ListJurisdictionAmounts { get; set; }

    /// <summary>
    /// Gets or sets the prefix of the names pay codes report to a field by, such as W2: for W2:12D. Empty uses the form's
    /// name and a colon.
    /// </summary>
    public string PayCodeFieldPrefix { get; set; }
}

/// <summary>
/// Whose pay a payroll tax form reports.
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
/// Reads Payroll's settings of a tax form.
/// </summary>
public static class PayrollTaxFormSettingsExtensions
{
    /// <summary>
    /// Gets Payroll's settings of a form: the stored ones, else the defaults (employees, no jurisdiction).
    /// </summary>
    /// <param name="form">The form.</param>
    /// <returns>The settings.</returns>
    public static PayrollTaxFormSettings GetPayrollSettings(this TaxForm form)
    {
        ArgumentNullException.ThrowIfNull(form);

        return form.TryGet<PayrollTaxFormSettings>(out var stored) && stored is not null ? stored : new PayrollTaxFormSettings();
    }

    /// <summary>
    /// Gets the prefix of the names pay codes report to a form's fields by: its own, else the form's name and a colon.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <returns>The prefix.</returns>
    public static string PayCodeFieldPrefix(this TaxForm form)
    {
        ArgumentNullException.ThrowIfNull(form);

        var prefix = form.GetPayrollSettings().PayCodeFieldPrefix;

        return string.IsNullOrWhiteSpace(prefix) ? form.Name + ":" : prefix.Trim();
    }
}
