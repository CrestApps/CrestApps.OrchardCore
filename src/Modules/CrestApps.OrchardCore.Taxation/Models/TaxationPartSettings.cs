namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Configures the behavior of a <see cref="TaxationPart"/> attached to a content type.
/// </summary>
public sealed class TaxationPartSettings
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
    /// Gets or sets a value indicating whether the tax classification fields are shown to editors.
    /// When disabled, only the default codes configured here are used.
    /// </summary>
    public bool AllowClassificationOverride { get; set; } = true;
}
