using CrestApps.OrchardCore.AdminTheme.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AdminTheme.ViewModels;

public class AdminThemeSettingsViewModel
{
    public string DefaultColorScheme { get; set; }

    [BindNever]
    public IReadOnlyList<AdminColorScheme> Schemes { get; set; } = [];
}
