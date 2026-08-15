using System;
using System.Collections.Generic;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Represents a tax category or classification. Categories form an open hierarchy through
/// <see cref="ParentCode"/> and are independent of the underlying object type. External codes map
/// the category to provider-specific tax systems.
/// </summary>
public sealed class TaxCategory : CatalogItem, INameAwareModel, IModifiedUtcAwareModel, ICloneable<TaxCategory>
{
    /// <inheritdoc />
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the unique code of the category (for example <c>Electronics</c> or <c>Television</c>).
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the code of the parent category, forming a hierarchy.
    /// </summary>
    public string ParentCode { get; set; }

    /// <summary>
    /// Gets or sets a description of the category.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the external, provider-specific tax codes keyed by provider name.
    /// </summary>
    public IDictionary<string, string> ExternalCodes { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public DateTime? ModifiedUtc { get; set; }

    /// <inheritdoc />
    public TaxCategory Clone()
    {
        return new TaxCategory
        {
            ItemId = ItemId,
            Name = Name,
            Code = Code,
            ParentCode = ParentCode,
            Description = Description,
            ExternalCodes = new Dictionary<string, string>(ExternalCodes, StringComparer.OrdinalIgnoreCase),
            ModifiedUtc = ModifiedUtc,
        };
    }
}
