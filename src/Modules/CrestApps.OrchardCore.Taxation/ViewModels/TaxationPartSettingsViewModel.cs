namespace CrestApps.OrchardCore.Taxation.ViewModels;

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
}
