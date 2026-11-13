using CrestApps.OrchardCore.SiteResources.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.SiteResources.ViewModels;

public class SiteResourceViewModel
{
    public string Name { get; set; }

    public string Description { get; set; }

    public SiteResourceType Type { get; set; }

    public string Content { get; set; }

    public bool Minify { get; set; }

    public SiteResourceTarget Target { get; set; }

    public SiteResourcePlacement Placement { get; set; }

    public SiteResourceLocation Location { get; set; }

    public string Dependencies { get; set; }

    public string[] Themes { get; set; } = [];

    public int Position { get; set; }

    public bool Enabled { get; set; }

    [BindNever]
    public bool IsNew { get; set; }

    [BindNever]
    public string Url { get; set; }

    [BindNever]
    public int ServedLength { get; set; }

    [BindNever]
    public IList<SelectListItem> TargetOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> PlacementOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> LocationOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> ThemeOptions { get; set; } = [];
}
