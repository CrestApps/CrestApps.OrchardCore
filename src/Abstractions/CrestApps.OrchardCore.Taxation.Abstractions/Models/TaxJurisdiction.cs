using System;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Represents a tax jurisdiction. Jurisdictions form an open hierarchy through
/// <see cref="ParentId"/> so that different countries can model different levels.
/// </summary>
public sealed class TaxJurisdiction : CatalogItem, INameAwareModel, IModifiedUtcAwareModel, ICloneable<TaxJurisdiction>
{
    /// <inheritdoc />
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the code of the jurisdiction.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the administrative level of the jurisdiction.
    /// </summary>
    public JurisdictionLevel Level { get; set; }

    /// <summary>
    /// Gets or sets the ISO country code the jurisdiction belongs to.
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// Gets or sets the state, province, or region code the jurisdiction belongs to.
    /// </summary>
    public string Region { get; set; }

    /// <summary>
    /// Gets or sets the county the jurisdiction belongs to.
    /// </summary>
    public string County { get; set; }

    /// <summary>
    /// Gets or sets the city the jurisdiction belongs to.
    /// </summary>
    public string City { get; set; }

    /// <summary>
    /// Gets or sets the postal code the jurisdiction covers.
    /// </summary>
    public string PostalCode { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the parent jurisdiction, forming a hierarchy.
    /// </summary>
    public string ParentId { get; set; }

    /// <summary>
    /// Gets or sets the UTC date the jurisdiction becomes effective.
    /// </summary>
    public DateTime? EffectiveFromUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC date the jurisdiction stops being effective.
    /// </summary>
    public DateTime? EffectiveToUtc { get; set; }

    /// <inheritdoc />
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC date the jurisdiction was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the name of the user that authored the jurisdiction.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user that owns the jurisdiction.
    /// </summary>
    public string OwnerId { get; set; }

    /// <inheritdoc />
    public TaxJurisdiction Clone()
    {
        return new TaxJurisdiction
        {
            ItemId = ItemId,
            Name = Name,
            Code = Code,
            Level = Level,
            Country = Country,
            Region = Region,
            County = County,
            City = City,
            PostalCode = PostalCode,
            ParentId = ParentId,
            EffectiveFromUtc = EffectiveFromUtc,
            EffectiveToUtc = EffectiveToUtc,
            CreatedUtc = CreatedUtc,
            ModifiedUtc = ModifiedUtc,
            Author = Author,
            OwnerId = OwnerId,
        };
    }
}
