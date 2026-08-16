using System;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Taxation.ViewModels;

/// <summary>
/// Represents the data used to create or edit a tax jurisdiction.
/// </summary>
public class TaxJurisdictionViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the tax jurisdiction is being created.
    /// </summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets the display name of the tax jurisdiction.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the unique code that identifies the tax jurisdiction.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the geographic level of the tax jurisdiction.
    /// </summary>
    public JurisdictionLevel Level { get; set; }

    /// <summary>
    /// Gets or sets the country for the tax jurisdiction.
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// Gets or sets the region or state for the tax jurisdiction.
    /// </summary>
    public string Region { get; set; }

    /// <summary>
    /// Gets or sets the county for the tax jurisdiction.
    /// </summary>
    public string County { get; set; }

    /// <summary>
    /// Gets or sets the city for the tax jurisdiction.
    /// </summary>
    public string City { get; set; }

    /// <summary>
    /// Gets or sets the postal code for the tax jurisdiction.
    /// </summary>
    public string PostalCode { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the parent tax jurisdiction.
    /// </summary>
    public string ParentId { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when the tax jurisdiction becomes effective.
    /// </summary>
    public DateTime? EffectiveFromUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when the tax jurisdiction stops being effective.
    /// </summary>
    public DateTime? EffectiveToUtc { get; set; }

    /// <summary>
    /// Gets or sets the available jurisdiction level options.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Levels { get; set; } = [];

    /// <summary>
    /// Gets or sets the available parent jurisdiction options.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Parents { get; set; } = [];
}
