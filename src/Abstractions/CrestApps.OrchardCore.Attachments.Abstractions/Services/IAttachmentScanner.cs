using CrestApps.OrchardCore.Attachments.Models;

namespace CrestApps.OrchardCore.Attachments.Services;

/// <summary>
/// Checks a file before it is attached, such as for viruses. Register one with
/// <see cref="AttachmentsServiceCollectionExtensions.AddAttachmentScanner{TScanner}"/>; every scanner must accept a file.
/// </summary>
public interface IAttachmentScanner
{
    /// <summary>
    /// Checks a file.
    /// </summary>
    /// <param name="upload">The file.</param>
    /// <param name="contentType">The media type the file's bytes show.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Why the file is refused, or <see langword="null"/> when it is accepted.</returns>
    Task<string> ScanAsync(AttachmentUpload upload, string contentType, CancellationToken cancellationToken = default);
}
