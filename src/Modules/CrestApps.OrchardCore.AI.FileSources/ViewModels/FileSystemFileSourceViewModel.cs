using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.FileSources.ViewModels;

/// <summary>
/// View model for the file-system connector settings.
/// </summary>
public class FileSystemFileSourceViewModel
{
    /// <summary>
    /// Gets or sets the folder to read, relative to this tenant's file-source folder. Empty means the
    /// tenant folder itself.
    /// </summary>
    public string RootPath { get; set; }

    /// <summary>
    /// Gets or sets the file pattern to match.
    /// </summary>
    public string SearchPattern { get; set; } = "*.*";

    /// <summary>
    /// Gets or sets a value indicating whether sub-folders are read too.
    /// </summary>
    public bool Recursive { get; set; } = true;

    /// <summary>
    /// Gets or sets the most files one listing will take on. Empty uses the host default.
    /// </summary>
    public int? MaxItems { get; set; }

    /// <summary>
    /// Gets or sets the absolute path of this tenant's file-source folder, shown so a reader knows where to
    /// put the files.
    /// </summary>
    [BindNever]
    public string TenantRootPath { get; set; }

    /// <summary>
    /// Gets or sets whether the resolved folder currently holds no files.
    /// </summary>
    [BindNever]
    public bool IsEmpty { get; set; }
}
