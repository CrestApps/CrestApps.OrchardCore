using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

/// <summary>
/// Represents the view model for edit chat interaction data source.
/// </summary>
public class EditChatInteractionDataSourceViewModel
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
    /// Gets or sets a value indicating whether is in scope.
    /// </summary>
    public bool IsInScope { get; set; }

    /// <summary>
    /// Gets or sets the filter.
    /// </summary>
    public string Filter { get; set; }

    /// <summary>
    /// Gets or sets the data sources.
    /// </summary>
    [BindNever]
    public IEnumerable<AIDataSource> DataSources { get; set; }
}
