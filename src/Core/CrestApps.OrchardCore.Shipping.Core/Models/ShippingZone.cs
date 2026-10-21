using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Shipping.Models;

/// <summary>
/// Where a set of shipping methods applies: countries, optionally narrowed to some of their regions. Zones are tried in
/// order of <see cref="Position"/>, and the first that matches an address wins; a zone for the rest of the world
/// catches any address no other zone matches.
/// </summary>
public sealed class ShippingZone : CatalogItem, INameAwareModel, IAuditedModel, ICloneable<ShippingZone>
{
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the zone's place in the order zones are tried, smallest first.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 codes of the countries the zone covers.
    /// </summary>
    public List<string> Countries { get; set; } = [];

    /// <summary>
    /// Gets or sets the ISO 3166-2 codes (such as <c>US-AK</c>) of the regions the zone is narrowed to. A country with
    /// no region listed here is covered whole.
    /// </summary>
    public List<string> Regions { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the zone covers every address no other zone matches.
    /// </summary>
    public bool RestOfWorld { get; set; }

    /// <summary>
    /// Gets or sets the methods a buyer in the zone can choose from.
    /// </summary>
    public List<ShippingMethod> Methods { get; set; } = [];

    public DateTime CreatedUtc { get; set; }

    public DateTime? ModifiedUtc { get; set; }

    public string Author { get; set; }

    public string OwnerId { get; set; }

    public ShippingMethod FindMethod(string methodId)
        => string.IsNullOrEmpty(methodId) ? null : Methods.FirstOrDefault(method => method.MethodId == methodId);

    public ShippingZone Clone()
        => JsonSerializer.Deserialize<ShippingZone>(JsonSerializer.Serialize(this));
}
