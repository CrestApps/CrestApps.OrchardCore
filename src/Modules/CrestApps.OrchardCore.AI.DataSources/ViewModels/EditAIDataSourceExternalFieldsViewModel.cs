using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.DataSources.ViewModels;

/// <summary>
/// View model for editing field mappings on non-index-profile data sources.
/// </summary>
public class EditAIDataSourceExternalFieldsViewModel
{
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
    [Required(AllowEmptyStrings = false)]
    public string ContentFieldName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the configuration is locked.
    /// </summary>
    [BindNever]
    public bool IsConfigurationLocked { get; set; }
}
