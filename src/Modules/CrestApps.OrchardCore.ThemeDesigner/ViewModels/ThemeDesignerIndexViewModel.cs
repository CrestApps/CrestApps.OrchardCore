using CrestApps.OrchardCore.ThemeDesigner.Models;
using CrestApps.OrchardCore.ThemeDesigner.Services;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.ThemeDesigner.ViewModels;

public class ThemeDesignerIndexViewModel
{
    public ThemeDesignDraft Draft { get; set; }

    public ActiveThemes Themes { get; set; }

    public AdminListContext<ThemeDesignChange> List { get; set; }
}
