namespace CrestApps.OrchardCore.Taxation.ViewModels;

using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

/// <summary>
/// The editor view model for a <see cref="Models.TaxationPart"/>.
/// </summary>
public class TaxationPartViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the content item is taxable.
    /// </summary>
    public bool Taxable { get; set; }

    /// <summary>
    /// Gets or sets the tax category code.
    /// </summary>
    public string TaxCategoryCode { get; set; }

    /// <summary>
    /// Gets or sets the tax classification code.
    /// </summary>
    public string TaxClassificationCode { get; set; }

    /// <summary>
    /// Gets or sets the external or provider-specific tax code.
    /// </summary>
    public string ExternalTaxCode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether editors can override the classification codes.
    /// </summary>
    public bool AllowClassificationOverride { get; set; }

    /// <summary>
    /// Gets or sets the list of available tax categories used to populate the category and
    /// classification dropdowns.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TaxCategories { get; set; } = [];
}
