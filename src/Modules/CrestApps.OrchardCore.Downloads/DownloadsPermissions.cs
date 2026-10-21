using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Downloads;

public static class DownloadsPermissions
{
    public static readonly Permission ManageDownloads = new(
        "ManageDigitalDownloads",
        LocalizationSource.Create("Manage digital files and downloads", typeof(DownloadsPermissions)));
}
