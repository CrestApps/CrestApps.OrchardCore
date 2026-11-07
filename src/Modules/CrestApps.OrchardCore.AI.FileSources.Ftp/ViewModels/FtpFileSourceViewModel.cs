using CrestApps.OrchardCore.AI.FileTransfer.Core.ViewModels;

namespace CrestApps.OrchardCore.AI.FileSources.Ftp.ViewModels;

/// <summary>
/// View model for the FTP/FTPS connector settings: the shared connection fields and the folder to read.
/// </summary>
public class FtpFileSourceViewModel : FtpConnectionFieldsViewModel
{
    /// <summary>
    /// Gets or sets the folder on the server to read.
    /// </summary>
    public string RemoteRootPath { get; set; } = "/";

    /// <summary>
    /// Gets or sets a value indicating whether sub-folders are read too.
    /// </summary>
    public bool RemoteRecursive { get; set; } = true;

    /// <summary>
    /// Gets or sets the most files one listing will take on. Empty uses the host default.
    /// </summary>
    public int? RemoteMaxItems { get; set; }
}
