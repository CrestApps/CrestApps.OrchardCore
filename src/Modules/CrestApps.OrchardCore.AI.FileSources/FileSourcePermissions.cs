using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.FileSources;

/// <summary>
/// Permissions provided by the File Sources feature.
/// </summary>
public static class FileSourcePermissions
{
    /// <summary>
    /// Permission that allows managing file sources (create, edit, delete, and run).
    /// </summary>
    public static readonly Permission ManageFileSources = new("ManageFileSources", "Manage file sources");
}
