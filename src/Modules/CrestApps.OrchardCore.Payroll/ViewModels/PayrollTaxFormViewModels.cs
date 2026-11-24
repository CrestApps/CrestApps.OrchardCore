using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Payroll.ViewModels;

/// <summary>
/// The tax form editor.
/// </summary>
public class PayrollTaxFormViewModel
{
    public string Name { get; set; }

    public string DisplayName { get; set; }

    public string Description { get; set; }

    public string Jurisdiction { get; set; }

    public string Period { get; set; }

    public string Workers { get; set; }

    public string PeopleJurisdiction { get; set; }

    public bool HasPersonStatements { get; set; }

    public bool StatementPerJurisdiction { get; set; }

    public bool ListJurisdictionAmounts { get; set; }

    public string PersonCondition { get; set; }

    public string MissingTaxIdNote { get; set; }

    public string PayCodeFieldPrefix { get; set; }

    public bool Enabled { get; set; }

    public string StatementName { get; set; }

    public string StatementTitle { get; set; }

    public string StatementCopy { get; set; }

    public string TaxIdLabel { get; set; }

    public string TaxIdFormat { get; set; }

    public string EmployerTaxIdLabel { get; set; }

    public string EmployerAccountJurisdiction { get; set; }

    public string FileName { get; set; }

    public IList<PayrollTaxFormFieldViewModel> Fields { get; set; } = [];

    public IList<PayrollTaxFormValueViewModel> Values { get; set; } = [];

    public IList<PayrollTaxFormCheckViewModel> Checks { get; set; } = [];

    /// <summary>
    /// Gets or sets whether a filing of the form was filed, so its name is kept.
    /// </summary>
    [BindNever]
    public bool IsFiled { get; set; }

    /// <summary>
    /// Gets or sets the functions formulas can call, for the help card: their category, signature and description.
    /// </summary>
    [BindNever]
    public IList<(string Category, string Signature, string Description)> Functions { get; set; } = [];
}

/// <summary>
/// A field of the tax form editor: one of the form's boxes or lines.
/// </summary>
public class PayrollTaxFormFieldViewModel
{
    public string Number { get; set; }

    public string Label { get; set; }

    public string Level { get; set; }

    public string Formula { get; set; }

    public bool AddToFiling { get; set; }

    public bool PayCodes { get; set; }

    public string Print { get; set; }
}

/// <summary>
/// A yearly value row of the tax form editor.
/// </summary>
public class PayrollTaxFormValueViewModel
{
    public string Name { get; set; }

    public int? Year { get; set; }

    public decimal? Amount { get; set; }
}

/// <summary>
/// A check row of the tax form editor.
/// </summary>
public class PayrollTaxFormCheckViewModel
{
    public string Condition { get; set; }

    public string Note { get; set; }
}
