using System;
using System.Collections.Generic;

namespace CrestApps.OrchardCore.Taxation.ViewModels;

/// <summary>
/// Represents the data used to create or edit a tax table.
/// </summary>
public class TaxTableViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the tax table is being created.
    /// </summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets the name of the tax table. This value is the unique key and is fixed after creation.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when the tax table becomes effective.
    /// </summary>
    public DateTime? EffectiveFromUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when the tax table stops being effective.
    /// </summary>
    public DateTime? EffectiveToUtc { get; set; }

    /// <summary>
    /// Gets or sets the rows that make up the tax table.
    /// </summary>
    public IList<TaxTableRowViewModel> Rows { get; set; } = [];
}
