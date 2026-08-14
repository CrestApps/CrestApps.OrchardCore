namespace CrestApps.OrchardCore.ContentTransfer.Models;

public sealed class ContentImportOptions
{
    public const int DefaultImportBatchSize = 100;

    public const int DefaultExportBatchSize = 200;

    /// <summary>
    /// The default maximum size, in bytes, of an uploaded import file. Defaults to 1 GB.
    /// </summary>
    public const long DefaultMaxUploadFileSize = 1_073_741_824;

    /// <summary>
    /// The default chunk size, in bytes, used when uploading import files. Defaults to 25 MB.
    /// This stays below the common pre-application request-body limit (about 28.6 MB /
    /// <c>30000000</c> bytes) enforced by IIS request filtering and several reverse proxies, so the
    /// default upload works out of the box on those hosts without extra configuration.
    /// </summary>
    public const int DefaultMaxUploadChunkSize = 26_214_400;

    public int ImportBatchSize { get; set; } = DefaultImportBatchSize;

    public bool AllowAllContentTypes { get; set; } = true;

    public string[] AllowedContentTypes { get; set; } = [];

    public int ExportBatchSize { get; set; } = DefaultExportBatchSize;

    /// <summary>
    /// The number of records at which exports are queued for background processing
    /// instead of being downloaded immediately.
    /// </summary>
    public int ExportQueueThreshold { get; set; } = 500;

    /// <summary>
    /// The maximum size, in bytes, of a file that can be uploaded for bulk import. This limit is
    /// independent from the global media library limit so large imports can be allowed without
    /// weakening media upload restrictions. Set to <c>0</c> to disable the size check.
    /// </summary>
    public long MaxUploadFileSize { get; set; } = DefaultMaxUploadFileSize;

    /// <summary>
    /// The maximum chunk size, in bytes, used when uploading import files. When an upload exceeds this
    /// value the file is streamed to the server in chunks so a single request body stays bounded. Set
    /// to <c>0</c> to disable chunked uploads and require the whole file in a single request.
    /// </summary>
    public int MaxUploadChunkSize { get; set; } = DefaultMaxUploadChunkSize;

    /// <summary>
    /// The lifetime of the temporary files created while assembling chunked uploads. Abandoned
    /// uploads older than this value are purged by a background task. Defaults to 1 hour.
    /// </summary>
    public TimeSpan TemporaryFileLifetime { get; set; } = TimeSpan.FromHours(1);
}
