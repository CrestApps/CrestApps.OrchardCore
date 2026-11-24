using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Indexes;

/// <summary>
/// Finds external tools by name and client id.
/// </summary>
public sealed class ExternalToolIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the client id of the tool's OpenID application.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets whether the tool can be launched.
    /// </summary>
    public bool Enabled { get; set; }
}
