using CrestApps.OrchardCore.AdminTheme.Models;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.AdminTheme.ViewModels;

public class AdminColorSchemesIndexViewModel
{
    public AdminListContext<AdminColorScheme> List { get; set; }
}
