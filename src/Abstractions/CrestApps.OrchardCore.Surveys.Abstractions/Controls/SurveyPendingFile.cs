namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// A file read from a posted page that is not stored yet. The handler puts its <see cref="Id"/> in the answer's
/// attachment ids; the runtime stores the file once the page passes validation and puts the stored attachment's id in
/// its place, or drops it when the page is refused.
/// </summary>
public sealed class SurveyPendingFile
{
    /// <summary>
    /// The prefix of the ids of pending files.
    /// </summary>
    public const string IdPrefix = "pending:";

    /// <summary>
    /// Gets or sets the temporary id the answer refers to until the file is stored.
    /// </summary>
    public string Id { get; set; } = IdPrefix + Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets the file name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the content type the client declared.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the bytes.
    /// </summary>
    public ReadOnlyMemory<byte> Content { get; set; }
}
