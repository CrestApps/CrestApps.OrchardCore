namespace CrestApps.OrchardCore.DncRegistry.Models;

/// <summary>
/// Represents a local do-not-call list uploaded by an administrator.
/// Each list is associated with a specific country and contains phone numbers
/// that should be excluded from outbound contact.
/// </summary>
public sealed class LocalDncList
{
    /// <summary>
    /// Gets or sets the unique identifier for this list.
    /// </summary>
    public string ListId { get; set; }

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 country code this list applies to.
    /// </summary>
    public string CountryCode { get; set; }

    /// <summary>
    /// Gets or sets the display name of this list.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the original uploaded file name.
    /// </summary>
    public string UploadedFileName { get; set; }

    /// <summary>
    /// Gets or sets the tenant-local stored file name used during background processing.
    /// </summary>
    public string StoredFileName { get; set; }

    /// <summary>
    /// Gets or sets the total number of phone numbers in this list.
    /// </summary>
    public int PhoneNumberCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of rows discovered in the uploaded file.
    /// </summary>
    public int TotalRecords { get; set; }

    /// <summary>
    /// Gets or sets the total number of rows already processed.
    /// </summary>
    public int TotalProcessed { get; set; }

    /// <summary>
    /// Gets or sets the total number of successfully imported phone numbers.
    /// </summary>
    public int ImportedCount { get; set; }

    /// <summary>
    /// Gets or sets the row-level validation and parsing errors.
    /// </summary>
    public Dictionary<int, string> ErrorMessages { get; set; }

    /// <summary>
    /// Gets or sets the current import status.
    /// </summary>
    public LocalDncListStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the error message when processing fails.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when this list was uploaded.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when import progress was last saved.
    /// </summary>
    public DateTime? ProcessSaveUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when the list finished processing.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }
}
