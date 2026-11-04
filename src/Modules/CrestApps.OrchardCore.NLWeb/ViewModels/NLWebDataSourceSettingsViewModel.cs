using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.NLWeb.ViewModels;

/// <summary>
/// The editor of the AI data source NLWeb searches by meaning.
/// </summary>
public class NLWebDataSourceSettingsViewModel
{
    /// <summary>
    /// Gets or sets the identifier of the AI data source, or empty for none.
    /// </summary>
    public string DataSourceId { get; set; }

    /// <summary>
    /// Gets or sets the AI data sources to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> DataSources { get; set; } = [];
}
