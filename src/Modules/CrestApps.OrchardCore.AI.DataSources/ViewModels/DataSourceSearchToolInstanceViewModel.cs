using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.DataSources.ViewModels;

/// <summary>
/// Represents the source specific fields captured for a data source search tool instance.
/// </summary>
public class DataSourceSearchToolInstanceViewModel
{
    /// <summary>
    /// Gets or sets the identifier of the data source the instance searches.
    /// </summary>
    public string DataSourceId { get; set; }

    /// <summary>
    /// Gets or sets the retrieval mode applied to every search the instance runs.
    /// </summary>
    public DataSourceRetrievalMode RetrievalMode { get; set; }

    /// <summary>
    /// Gets or sets the number of top-scoring documents to retrieve.
    /// </summary>
    public int? TopNDocuments { get; set; }

    /// <summary>
    /// Gets or sets the strictness threshold used to decide how relevant a result must be.
    /// </summary>
    public int? Strictness { get; set; }

    /// <summary>
    /// Gets or sets the OData filter expression that narrows the search.
    /// </summary>
    public string Filter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the model must answer only from the retrieved content.
    /// </summary>
    public bool IsInScope { get; set; }

    /// <summary>
    /// Gets or sets the data sources offered in the data source list.
    /// </summary>
    [BindNever]
    public IEnumerable<AIDataSource> DataSources { get; set; } = [];
}
