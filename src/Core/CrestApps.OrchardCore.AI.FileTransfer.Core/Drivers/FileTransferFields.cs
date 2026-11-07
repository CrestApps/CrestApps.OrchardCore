namespace CrestApps.OrchardCore.AI.FileTransfer.Core.Drivers;

/// <summary>
/// Small helpers the FTP and SFTP connection editors share.
/// </summary>
internal static class FileTransferFields
{
    /// <summary>
    /// Trims a posted value and stores a blank one as <see langword="null"/>.
    /// </summary>
    /// <param name="value">The posted value.</param>
    /// <returns>The value to store.</returns>
    public static string Trimmed(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
