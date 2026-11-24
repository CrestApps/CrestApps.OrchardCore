using System.Security.Claims;
using CrestApps.Core.Infrastructure.Indexing;

namespace CrestApps.OrchardCore.AI.Core.DataSources;

/// <summary>
/// The request a data source search runs for, as an <see cref="IAIDataSourceResultFilter"/> sees it.
/// </summary>
public sealed class AIDataSourceResultFilterContext
{
    /// <summary>
    /// Gets or sets the knowledge base index that was searched.
    /// </summary>
    public IIndexProfileInfo IndexProfile { get; set; }

    /// <summary>
    /// Gets or sets the data source that was searched.
    /// </summary>
    public string DataSourceId { get; set; }

    /// <summary>
    /// Gets or sets the person the AI answers, or <see langword="null"/> when the search does not run for a request.
    /// </summary>
    public ClaimsPrincipal User { get; set; }

    /// <summary>
    /// Gets or sets the resource of the completion, usually an AI profile or a chat interaction, or
    /// <see langword="null"/> when the search runs outside a completion.
    /// </summary>
    public object Resource { get; set; }

    /// <summary>
    /// Gets or sets the AI profile of the chat session, when the search runs in one.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the token that cancels the search.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }
}
