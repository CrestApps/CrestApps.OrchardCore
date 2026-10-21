namespace CrestApps.OrchardCore.Downloads.Models;

/// <summary>
/// A buyer's right to download one file of a product they bought on one order.
/// </summary>
public sealed class DownloadEntitlement
{
    public long Id { get; set; }

    public string ItemId { get; set; }

    public string OrderId { get; set; }

    public string OrderNumber { get; set; }

    public string OrderLineId { get; set; }

    public string ProductId { get; set; }

    public string ProductTitle { get; set; }

    public string FileId { get; set; }

    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets how many times the file can be downloaded, or <see langword="null"/> for no limit.
    /// </summary>
    public int? DownloadLimit { get; set; }

    public int DownloadCount { get; set; }

    /// <summary>
    /// Gets or sets when the link stops working, or <see langword="null"/> when it never does.
    /// </summary>
    public DateTime? ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets when the right was taken back, such as when the order was canceled or refunded.
    /// </summary>
    public DateTime? RevokedUtc { get; set; }

    public string RevokeReason { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? LastDownloadedUtc { get; set; }

    /// <summary>
    /// Gets how many downloads are left, or <see langword="null"/> when there is no limit.
    /// </summary>
    public int? Remaining
        => DownloadLimit.HasValue ? Math.Max(0, DownloadLimit.Value - DownloadCount) : null;
}

/// <summary>
/// Why a download link doesn't work.
/// </summary>
public enum DownloadProblem
{
    None = 0,

    NotFound = 1,

    Revoked = 2,

    Expired = 3,

    LimitReached = 4,
}
