namespace CrestApps.OrchardCore.ContentTransfer.Services;

/// <summary>
/// Describes why a chunked import upload request was rejected.
/// </summary>
public enum ContentTransferUploadError
{
    /// <summary>
    /// The upload request was malformed, for example an invalid content range or upload identifier.
    /// </summary>
    InvalidRequest,

    /// <summary>
    /// The total size of the uploaded file exceeds the configured maximum.
    /// </summary>
    MaxFileSizeExceeded,

    /// <summary>
    /// A single uploaded chunk exceeds the configured maximum chunk size.
    /// </summary>
    MaxChunkSizeExceeded,
}
