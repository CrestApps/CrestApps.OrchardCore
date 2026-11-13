using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Attachments.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.Attachments.Services;

/// <summary>
/// Runs every attached file through Orchard Core's <see cref="FileCreationService"/>, so each registered
/// <see cref="IFileEventHandler"/>, such as the antivirus handler, can refuse it before it is stored.
/// </summary>
internal sealed class FileCreationAttachmentScanner : IAttachmentScanner
{
    private readonly FileCreationService _fileCreationService;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileCreationAttachmentScanner"/> class.
    /// </summary>
    /// <param name="fileCreationService">The Orchard Core file creation service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FileCreationAttachmentScanner(
        FileCreationService fileCreationService,
        ILogger<FileCreationAttachmentScanner> logger,
        IStringLocalizer<FileCreationAttachmentScanner> stringLocalizer)
    {
        _fileCreationService = fileCreationService;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<string> ScanAsync(AttachmentUpload upload, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);

        try
        {
            await using var content = new MemoryStream(upload.Content.ToArray(), writable: false);
            await using var result = await _fileCreationService.CreateAsync(
                new FileCreatingContext(upload.FileName, upload.Content.Length, contentType),
                content,
                leaveOpen: true,
                cancellationToken);

            if (result.Succeeded)
            {
                return null;
            }

            _logger.LogWarning("An attached file was refused by the file scanners: {Reason}", result.ErrorMessage);

            return string.IsNullOrWhiteSpace(result.ErrorMessage) ? S["the file scanner refused it"] : result.ErrorMessage;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Scanning an attached file failed.");

            return S["it could not be scanned"];
        }
    }
}
