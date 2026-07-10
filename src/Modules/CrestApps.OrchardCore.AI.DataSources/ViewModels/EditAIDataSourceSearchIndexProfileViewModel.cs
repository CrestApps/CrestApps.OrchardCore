using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.DataSources.ViewModels;

/// <summary>
/// View model for editing Search Index Profile source settings.
/// </summary>
public class EditAIDataSourceSearchIndexProfileViewModel
{
    /// <summary>
    /// Gets or sets the source index profile name.
    /// </summary>
    public string SourceIndexProfileName { get; set; }

    /// <summary>
    /// Gets or sets the key field name.
    /// </summary>
    public string KeyFieldName { get; set; }

    /// <summary>
    /// Gets or sets the title field name.
    /// </summary>
    public string TitleFieldName { get; set; }

    /// <summary>
    /// Gets or sets the content field name.
    /// </summary>
    public string ContentFieldName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the configuration is locked.
    /// </summary>
    [BindNever]
    public bool IsConfigurationLocked { get; set; }

    /// <summary>
    /// Gets or sets the source index profile names.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> SourceIndexProfileNames { get; set; }

    /// <summary>
    /// Gets or sets the field names.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> FieldNames { get; set; }
}
