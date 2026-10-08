using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Tests.Validation;

/// <summary>
/// A minimal catalog entry used to exercise how validation failures are reported against an editor.
/// </summary>
public sealed class StubValidatedEntry : CatalogItem
{
    /// <summary>
    /// Gets or sets the entry name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the entry description.
    /// </summary>
    public string Description { get; set; }
}
