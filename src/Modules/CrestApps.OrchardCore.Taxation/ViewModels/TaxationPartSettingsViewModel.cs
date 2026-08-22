namespace CrestApps.OrchardCore.Taxation.ViewModels;

using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

/// <summary>
/// The settings editor view model for a <see cref="Models.TaxationPart"/>.
/// </summary>
public class TaxationPartSettingsViewModel
{
    /// <summary>
    /// Gets or sets the default tax category code applied to new content items.
    /// </summary>
    public string DefaultTaxCategoryCode { get; set; }

    /// <summary>
    /// Gets or sets the default tax classification code applied to new content items.
    /// </summary>
    public string DefaultTaxClassificationCode { get; set; }

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
