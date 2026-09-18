using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.DataSources.FileSources.Services;

/// <summary>
/// Adds the File Sources entry to the Artificial Intelligence admin menu.
/// </summary>
public sealed class FileSourceAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSourceAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FileSourceAdminMenu(IStringLocalizer<FileSourceAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], ai => ai
                .Add(S["File Sources"], S["File Sources"].PrefixPosition(), fileSources => fileSources
                    .AddClass("ai-file-sources")
                    .Id("aiFileSources")
                    .Action("Index", "FileSources", "CrestApps.OrchardCore.AI.DataSources.FileSources")
                    .Permission(FileSourcePermissions.ManageFileSources)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
