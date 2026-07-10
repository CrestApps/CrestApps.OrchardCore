using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.DataSources.ViewModels;

/// <summary>
/// View model for the top-level AI data source editor fields.
/// </summary>
public class EditAIDataSourceFieldsViewModel
{
    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the selected source type.
    /// </summary>
    public string SourceType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the index configuration is locked.
    /// </summary>
    [BindNever]
    public bool IsConfigurationLocked { get; set; }
}
