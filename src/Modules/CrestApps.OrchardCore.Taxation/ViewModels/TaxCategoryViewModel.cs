namespace CrestApps.OrchardCore.Taxation.ViewModels;

/// <summary>
/// Represents the data used to create or edit a tax category.
/// </summary>
public class TaxCategoryViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the tax category is being created.
    /// </summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets the display name of the tax category.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the unique code that identifies the tax category.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the code of the parent tax category.
    /// </summary>
    public string ParentCode { get; set; }

    /// <summary>
    /// Gets or sets the description of the tax category.
    /// </summary>
    public string Description { get; set; }
}
