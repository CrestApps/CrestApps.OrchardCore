using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.ContentTransfer.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContentTransfer.Services;

public sealed class ContentTransferChunkFileUploadService : IContentTransferChunkFileUploadService
{
    private const string UploadIdFormKey = "__chunkedFileUploadId";
    private const string TempFolderName = "CrestAppsContentTransferUploads";

    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly IOptions<ContentImportOptions> _options;
    private readonly string _tempFileNamePrefix;

    public ContentTransferChunkFileUploadService(
        ShellSettings shellSettings,
        IClock clock,
        ILogger<ContentTransferChunkFileUploadService> logger,
        IOptions<ContentImportOptions> options)
    {
        _clock = clock;
        _logger = logger;
        _options = options;
        _tempFileNamePrefix = shellSettings.Name + "_";
    }

    public async Task<IActionResult> ProcessRequestAsync(
        HttpRequest request,
        Func<Guid, IFormFile, ContentRangeHeaderValue, Task<IActionResult>> chunkAsync,
        Func<IEnumerable<IFormFile>, Task<IActionResult>> completedAsync,
        Func<ContentTransferUploadError, Task<IActionResult>> invalidAsync)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(chunkAsync);
        ArgumentNullException.ThrowIfNull(completedAsync);
        ArgumentNullException.ThrowIfNull(invalidAsync);

        var options = _options.Value;
        var contentRange = request.Headers.ContentRange;

        if (options.MaxUploadChunkSize <= 0 || contentRange.Count == 0 || !request.Form.TryGetValue(UploadIdFormKey, out var uploadIdValue))
        {
            return await completedAsync(request.Form.Files);
        }

        if (request.Form.Files.Count != 1
            || !ContentRangeHeaderValue.TryParse(contentRange, out var range)
            || !Guid.TryParse(uploadIdValue, out var uploadId)
            || !range.HasLength
            || !range.HasRange)
        {
            return await invalidAsync(ContentTransferUploadError.InvalidRequest);
        }

        if (options.MaxUploadFileSize > 0 && range.Length > options.MaxUploadFileSize)
        {
            return await invalidAsync(ContentTransferUploadError.MaxFileSizeExceeded);
        }

        if (range.To.Value - range.From.Value + 1 > options.MaxUploadChunkSize)
        {
            return await invalidAsync(ContentTransferUploadError.MaxChunkSizeExceeded);
        }

        var formFile = request.Form.Files[0];

        await using (var fileStream = GetOrCreateTemporaryFile(uploadId, formFile, range.Length.Value))
        {
            fileStream.Seek(range.From.Value, SeekOrigin.Begin);
            await formFile.CopyToAsync(fileStream, request.HttpContext.RequestAborted);
        }

        return range.To.Value + 1 < range.Length.Value
            ? await chunkAsync(uploadId, formFile, range)
            : await CompleteUploadAsync(uploadId, formFile, completedAsync);
    }

    public void PurgeTempDirectory()
    {
        var tempFolderPath = GetTempFolderPath();
        var lifetime = _options.Value.TemporaryFileLifetime;

        if (lifetime <= TimeSpan.Zero || !Directory.Exists(tempFolderPath))
        {
            return;
        }

        var staleFiles = Directory.GetFiles(tempFolderPath, _tempFileNamePrefix + "*")
            .Select(filePath => new FileInfo(filePath))
            .Where(fileInfo => fileInfo.LastWriteTimeUtc + lifetime < _clock.UtcNow);

        foreach (var fileInfo in staleFiles)
        {
            if (!DeleteTemporaryFile(fileInfo.FullName))
            {
                break;
            }
        }
    }

    private async Task<IActionResult> CompleteUploadAsync(
        Guid uploadId,
        IFormFile formFile,
        Func<IEnumerable<IFormFile>, Task<IActionResult>> completedAsync)
    {
        try
        {
            using var chunkedFormFile = GetTemporaryFileForRead(uploadId, formFile);

            return await completedAsync([chunkedFormFile]);
        }
        finally
        {
            DeleteTemporaryFile(GetTempFilePath(uploadId, formFile));
        }
    }

    private bool DeleteTemporaryFile(string tempFilePath)
    {
        try
        {
            File.Delete(tempFilePath);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while deleting the temporary content transfer upload file '{TempFilePath}'.", tempFilePath);
        }

        return false;
    }

    private FileStream GetOrCreateTemporaryFile(Guid uploadId, IFormFile formFile, long size)
    {
        var tempFolderPath = GetTempFolderPath();

        if (!Directory.Exists(tempFolderPath))
        {
            Directory.CreateDirectory(tempFolderPath);
        }

        var tempFilePath = GetTempFilePath(uploadId, formFile);

        if (File.Exists(tempFilePath))
        {
            return File.Open(tempFilePath, FileMode.Open, FileAccess.ReadWrite);
        }

        return CreateTemporaryFile(tempFilePath, size);
    }

    private ChunkedFormFile GetTemporaryFileForRead(Guid uploadId, IFormFile formFile)
        => new(File.OpenRead(GetTempFilePath(uploadId, formFile)))
        {
            ContentType = formFile.ContentType,
            ContentDisposition = formFile.ContentDisposition,
            Headers = formFile.Headers,
            Name = formFile.Name,
            FileName = formFile.FileName,
        };

    private static string GetTempFolderPath()
        => Path.Combine(Path.GetTempPath(), TempFolderName);

    private string GetTempFilePath(Guid uploadId, IFormFile formFile)
        => Path.Combine(GetTempFolderPath(), _tempFileNamePrefix + CalculateHash(uploadId.ToString("N"), formFile.FileName, formFile.Name));

    private static FileStream CreateTemporaryFile(string tempPath, long size)
    {
        var fileStream = File.Create(tempPath);
        fileStream.SetLength(size);
        new FileInfo(tempPath).Attributes |= FileAttributes.Temporary;

        return fileStream;
    }

    private static string CalculateHash(params string[] parts)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', parts)));

        return Convert.ToHexString(bytes);
    }

    private sealed class ChunkedFormFile : IFormFile, IDisposable
    {
        private readonly Stream _stream;
        private bool _disposed;

        public ChunkedFormFile(Stream stream)
        {
            _stream = stream;
        }

        public string ContentType { get; set; }

        public string ContentDisposition { get; set; }

        public IHeaderDictionary Headers { get; set; }

        public long Length => _stream.Length;

        public string Name { get; set; }

        public string FileName { get; set; }

        public void CopyTo(Stream target)
            => _stream.CopyTo(target);

        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
            => _stream.CopyToAsync(target, cancellationToken);

        public Stream OpenReadStream()
            => _stream;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stream?.Dispose();
        }
    }
}
