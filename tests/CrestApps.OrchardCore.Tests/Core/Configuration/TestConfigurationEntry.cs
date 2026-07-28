using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Tests.Core.Configuration;

/// <summary>
/// A catalog entry that exists only so the shared configuration catalog can be exercised without depending on the
/// rules of any particular module.
/// </summary>
public sealed class TestConfigurationEntry : CatalogItem, INameAwareModel
{
    /// <summary>
    /// Gets or sets the name that identifies the entry when the destination does not know its identifier.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a value a manager is free to derive rather than take from the plan verbatim.
    /// </summary>
    public string Description { get; set; }
}
