using CrestApps.OrchardCore.ThemeDesigner.Models;

namespace CrestApps.OrchardCore.ThemeDesigner.ViewModels;

public class ThemeDesignChangeViewModel
{
    public ThemeDesignChangeKind Kind { get; set; }

    public string Name { get; set; }

    public ThemeDesignChangeStatus? Status { get; set; }

    public string PublishedContent { get; set; }

    public string DraftContent { get; set; }
}
