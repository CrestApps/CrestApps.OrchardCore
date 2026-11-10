using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// A capability or extension the store offers in UCP.
/// </summary>
public sealed class UcpCapability
{
    /// <summary>
    /// Gets or sets its name, such as <c>dev.ucp.shopping.checkout</c>.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets its version.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets the address of its specification.
    /// </summary>
    public string Spec { get; set; }

    /// <summary>
    /// Gets or sets the address of its schema.
    /// </summary>
    public string Schema { get; set; }

    /// <summary>
    /// Gets or sets the capabilities it extends; empty for a root capability.
    /// </summary>
    public IList<string> Extends { get; set; } = [];

    /// <summary>
    /// Gets or sets its configuration in the store's profile, or <see langword="null"/>.
    /// </summary>
    public JsonObject Config { get; set; }
}
