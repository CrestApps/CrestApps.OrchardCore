using CrestApps.Core.AI.Documents;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.AI.Documents.Services;

/// <summary>
/// An <see cref="IUploadedFileScanner"/> implementation that routes uploaded files through Orchard Core's
/// <see cref="FileCreationService"/> pre-storage security pipeline so every registered
/// <see cref="IFileEventHandler"/> (such as the ClamAV antivirus handler) can inspect, reject, or replace
/// the file before it is stored or processed.
/// </summary>
public sealed class OrchardUploadedFileScanner : IUploadedFileScanner
{
    private readonly FileCreationService _fileCreationService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchardUploadedFileScanner"/> class.
    /// </summary>
    /// <param name="fileCreationService">The Orchard Core file creation service that coordinates the file event handlers.</param>
    /// <param name="logger">The logger used to record rejected or failed scans.</param>
    public OrchardUploadedFileScanner(
        FileCreationService fileCreationService,
        ILogger<OrchardUploadedFileScanner> logger)
    {
        _fileCreationService = fileCreationService;
        _logger = logger;
    }

    /// <summary>
    /// Scans the uploaded file by running it through the Orchard Core file creation pipeline without persisting it.
    /// </summary>
    /// <param name="file">The uploaded file to scan.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// <see cref="FileScanResult.Clean"/> when no handler rejected the file, an infected result when a handler
    /// rejected it, or an error result when the scan itself failed.
    /// </returns>
    public async Task<FileScanResult> ScanAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        try
        {
            await using var uploadedStream = file.OpenReadStream();
            await using var result = await _fileCreationService.CreateAsync(
                new FileCreatingContext(file.FileName, file.Length, file.ContentType),
                uploadedStream,
                leaveOpen: true,
                cancellationToken);

            if (!result.Succeeded)
            {
                var reason = result.ErrorMessage;

                if (string.IsNullOrWhiteSpace(reason))
                {
                    reason = $"The uploaded file '{file.FileName}' was rejected by the Orchard Core file creation pipeline.";
                }

                _logger.LogWarning("The uploaded file '{FileName}' was rejected by the Orchard Core file creation pipeline: {Reason}", file.FileName, reason);

                return FileScanResult.Infected(reason);
            }

            return FileScanResult.Clean;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while scanning the uploaded file '{FileName}' through the Orchard Core file creation pipeline.", file.FileName);

            return FileScanResult.Error($"Scanning the uploaded file '{file.FileName}' failed: {ex.Message}");
        }
    }
}
