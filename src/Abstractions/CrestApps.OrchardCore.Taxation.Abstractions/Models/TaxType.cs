using System;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Represents a user-managed tax type (for example <c>SalesTax</c>, <c>VAT</c>, or <c>GST</c>). Tax
/// types are grouping and reporting labels only; they never affect how tax is calculated. A tax rule
/// references a tax type by its <see cref="Name"/>, which is the value stored on the resulting tax
/// lines.
/// </summary>
public sealed class TaxType : CatalogItem, INameAwareModel, IModifiedUtcAwareModel, ICloneable<TaxType>
{
    /// <inheritdoc />
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets an optional description of the tax type.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the UTC date the tax type was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <inheritdoc />
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the name of the user that authored the tax type.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user that owns the tax type.
    /// </summary>
    public string OwnerId { get; set; }

    /// <inheritdoc />
    public TaxType Clone()
    {
        return new TaxType
        {
            ItemId = ItemId,
            Name = Name,
            Description = Description,
            CreatedUtc = CreatedUtc,
            ModifiedUtc = ModifiedUtc,
            Author = Author,
            OwnerId = OwnerId,
        };
    }
}
