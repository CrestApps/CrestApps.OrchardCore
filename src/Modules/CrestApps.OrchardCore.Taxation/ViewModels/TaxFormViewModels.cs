using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Taxation.ViewModels;

/// <summary>
/// The tax form editor.
/// </summary>
public class TaxFormViewModel
{
    public string Name { get; set; }

    public string DisplayName { get; set; }

    public string Description { get; set; }

    public string Source { get; set; }

    public string Jurisdiction { get; set; }

    public string Period { get; set; }

    public bool HasRecipientStatements { get; set; }

    public bool StatementPerJurisdiction { get; set; }

    public string RecipientCondition { get; set; }

    public string MissingTaxIdNote { get; set; }

    public bool Enabled { get; set; }

    public string StatementName { get; set; }

    public string StatementTitle { get; set; }

    public string StatementCopy { get; set; }

    public string TaxIdLabel { get; set; }

    public string TaxIdFormat { get; set; }

    public string FilerTaxIdLabel { get; set; }

    public string FilerAccountJurisdiction { get; set; }

    public string FileName { get; set; }

    public IList<TaxFormFieldViewModel> Fields { get; set; } = [];

    public IList<TaxFormValueViewModel> Values { get; set; } = [];

    public IList<TaxFormCheckViewModel> Checks { get; set; } = [];

    /// <summary>
    /// Gets or sets whether something uses the form, such as filings, so its name is kept.
    /// </summary>
    [BindNever]
    public bool IsUsed { get; set; }

    /// <summary>
    /// Gets or sets the sources a form can have.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Sources { get; set; } = [];

    /// <summary>
    /// Gets or sets what the source's records that report amounts to a field are called, such as pay codes; or
    /// <see langword="null"/> when the source has none.
    /// </summary>
    [BindNever]
    public string ReportedAmountsLabel { get; set; }

    /// <summary>
    /// Gets or sets the functions the form's formulas can call, for the field dialog: their category, signature and
    /// description.
    /// </summary>
    [BindNever]
    public IList<(string Category, string Signature, string Description)> Functions { get; set; } = [];
}

/// <summary>
/// A field of the tax form editor: one of the form's boxes or lines.
/// </summary>
public class TaxFormFieldViewModel
{
    public string Number { get; set; }

    public string Label { get; set; }

    public string Level { get; set; }

    public string Formula { get; set; }

    public bool AddToFiling { get; set; }

    public bool AcceptsReportedAmounts { get; set; }

    public string Print { get; set; }
}

/// <summary>
/// A yearly value row of the tax form editor.
/// </summary>
public class TaxFormValueViewModel
{
    public string Name { get; set; }

    public int? Year { get; set; }

    public decimal? Amount { get; set; }
}

/// <summary>
/// A check row of the tax form editor.
/// </summary>
public class TaxFormCheckViewModel
{
    public string Condition { get; set; }

    public string Note { get; set; }
}
