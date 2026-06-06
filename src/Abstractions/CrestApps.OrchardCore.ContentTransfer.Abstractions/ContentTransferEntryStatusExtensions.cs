namespace CrestApps.OrchardCore.ContentTransfer;

/// <summary>
/// Provides helpers for interpreting import-specific content transfer entry statuses.
/// </summary>
public static class ContentTransferEntryStatusExtensions
{
    /// <summary>
    /// Determines whether the status represents a queued import waiting to start.
    /// </summary>
    /// <param name="status">The status to evaluate.</param>
    /// <returns><c>true</c> when the import is pending; otherwise, <c>false</c>.</returns>
    public static bool IsPendingImport(this ContentTransferEntryStatus status)
        => status == ContentTransferEntryStatus.Pending
            || status == ContentTransferEntryStatus.New;

    /// <summary>
    /// Determines whether the status represents a paused import.
    /// </summary>
    /// <param name="status">The status to evaluate.</param>
    /// <returns><c>true</c> when the import is paused; otherwise, <c>false</c>.</returns>
    public static bool IsPausedImport(this ContentTransferEntryStatus status)
        => status == ContentTransferEntryStatus.Paused;

    /// <summary>
    /// Determines whether the status can be resumed for import processing.
    /// </summary>
    /// <param name="status">The status to evaluate.</param>
    /// <returns><c>true</c> when the import can be resumed; otherwise, <c>false</c>.</returns>
    public static bool CanResumeImport(this ContentTransferEntryStatus status)
        => status.IsPendingImport()
            || status.IsPausedImport()
            || status == ContentTransferEntryStatus.Failed;

    /// <summary>
    /// Determines whether the status should stop import processing.
    /// </summary>
    /// <param name="status">The status to evaluate.</param>
    /// <returns><c>true</c> when background import processing should stop; otherwise, <c>false</c>.</returns>
    public static bool ShouldStopImport(this ContentTransferEntryStatus status)
        => status.IsPausedImport()
            || status == ContentTransferEntryStatus.Deleting;

    /// <summary>
    /// Maps import statuses to the normalized status used by the admin UI.
    /// </summary>
    /// <param name="status">The status to normalize.</param>
    /// <returns>The normalized import status for display and filtering.</returns>
    public static ContentTransferEntryStatus NormalizeImportStatus(this ContentTransferEntryStatus status)
    {
        if (status.IsPendingImport())
        {
            return ContentTransferEntryStatus.Pending;
        }

        if (status.IsPausedImport())
        {
            return ContentTransferEntryStatus.Paused;
        }

        return status;
    }
}
