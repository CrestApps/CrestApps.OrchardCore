using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.DataSources.ViewModels;

/// <summary>
/// Represents the view model used to edit the data source configuration of the
/// AI completion with config workflow activity.
/// </summary>
public class AICompletionWithConfigDataSourceViewModel
{
    /// <summary>
    /// Gets or sets the data source id.
    /// </summary>
    public string DataSourceId { get; set; }

    /// <summary>
    /// Gets or sets the strictness.
    /// </summary>
    public int Strictness { get; set; }

    /// <summary>
    /// Gets or sets the top n documents.
    /// </summary>
    public int TopNDocuments { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether answers are restricted to retrieved data only.
    /// </summary>
    public bool IsInScope { get; set; }

    /// <summary>
    /// Gets or sets the filter.
    /// </summary>
    public string Filter { get; set; }

    /// <summary>
    /// Gets or sets the available data sources.
    /// </summary>
    [BindNever]
    public IEnumerable<AIDataSource> DataSources { get; set; }
}
