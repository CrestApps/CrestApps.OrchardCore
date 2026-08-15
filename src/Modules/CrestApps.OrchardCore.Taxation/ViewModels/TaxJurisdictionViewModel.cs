using System;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Taxation.ViewModels;

public sealed class TaxJurisdictionViewModel
{
    public bool IsNew { get; set; }

    public string Name { get; set; }

    public string Code { get; set; }

    public JurisdictionLevel Level { get; set; }

    public string Country { get; set; }

    public string Region { get; set; }

    public string County { get; set; }

    public string City { get; set; }

    public string PostalCode { get; set; }

    public string ParentId { get; set; }

    public DateTime? EffectiveFromUtc { get; set; }

    public DateTime? EffectiveToUtc { get; set; }

    [BindNever]
    public IList<SelectListItem> Levels { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> Parents { get; set; } = [];
}
