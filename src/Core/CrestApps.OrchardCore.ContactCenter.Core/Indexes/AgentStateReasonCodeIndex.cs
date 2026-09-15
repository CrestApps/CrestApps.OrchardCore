using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Represents the YesSql index used to query agent state reason codes.
/// </summary>
public sealed class AgentStateReasonCodeIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the unique reason code name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the relative order the reason code is listed in.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the reason code is enabled.
    /// </summary>
    public bool Enabled { get; set; }
}
