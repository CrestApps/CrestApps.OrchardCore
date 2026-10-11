namespace CrestApps.OrchardCore.Taxation.ViewModels;

/// <summary>
/// Represents the data used to create or edit a tax type.
/// </summary>
public class TaxTypeViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the tax type is being created.
    /// </summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets the name of the tax type. This value is stored on tax lines produced by rules
    /// that reference the type.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the optional description of the tax type.
    /// </summary>
    public string Description { get; set; }
}
