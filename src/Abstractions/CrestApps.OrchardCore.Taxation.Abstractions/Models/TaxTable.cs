using System;
using System.Collections.Generic;
using System.Linq;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// A first-class, versioned tax table used by table-driven calculation methods to model brackets,
/// progressive schedules, per-unit schedules, and lookup tables.
/// </summary>
public sealed class TaxTable : CatalogItem, INameAwareModel, IModifiedUtcAwareModel, ICloneable<TaxTable>
{
    /// <inheritdoc />
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the version of the table. The version increments on every update and is captured on tax
    /// lines for audit. Because a table is updated in place rather than kept as immutable revisions, preserve
    /// a historical calculation by publishing a new table with its own effective window (see
    /// <see cref="EffectiveFromUtc"/> and <see cref="EffectiveToUtc"/>) instead of mutating a table that
    /// dated transactions already used.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Gets or sets the UTC date the table becomes effective.
    /// </summary>
    public DateTime? EffectiveFromUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC date the table stops being effective.
    /// </summary>
    public DateTime? EffectiveToUtc { get; set; }

    /// <summary>
    /// Gets or sets the rows that make up the table.
    /// </summary>
    public IList<TaxTableRow> Rows { get; set; } = [];

    /// <inheritdoc />
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC date the table was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the name of the user that authored the table.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user that owns the table.
    /// </summary>
    public string OwnerId { get; set; }

    /// <inheritdoc />
    public TaxTable Clone()
    {
        return new TaxTable
        {
            ItemId = ItemId,
            Name = Name,
            Version = Version,
            EffectiveFromUtc = EffectiveFromUtc,
            EffectiveToUtc = EffectiveToUtc,
            Rows = Rows.Select(row => row.Clone()).ToList(),
            ModifiedUtc = ModifiedUtc,
            CreatedUtc = CreatedUtc,
            Author = Author,
            OwnerId = OwnerId,
        };
    }
}
