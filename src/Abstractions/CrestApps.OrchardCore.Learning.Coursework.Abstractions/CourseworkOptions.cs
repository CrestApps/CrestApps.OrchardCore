namespace CrestApps.OrchardCore.Learning.Coursework;

/// <summary>
/// The limits of coursework hand-ins, read from the <c>CrestApps:Learning:Coursework</c> configuration section. The
/// coursework editor offers values up to these limits, and a hand-in request may be as large as the largest hand-in
/// they allow.
/// </summary>
public sealed class CourseworkOptions
{
    /// <summary>
    /// The most files a coursework item may take when none is configured.
    /// </summary>
    public const int DefaultMaxFilesPerSubmission = 20;

    /// <summary>
    /// The largest file a coursework item may take when none is configured, in megabytes.
    /// </summary>
    public const int DefaultMaxFileSizeMb = 50;

    // Room for the text, the form fields and the multipart boundaries of a hand-in.
    private const long FormOverheadBytes = 16L * 1024 * 1024;

    /// <summary>
    /// Gets or sets the most files an author may let a learner hand in at once.
    /// </summary>
    public int MaxFilesPerSubmission { get; set; } = DefaultMaxFilesPerSubmission;

    /// <summary>
    /// Gets or sets the largest file, in megabytes, an author may let a learner hand in.
    /// </summary>
    public int MaxFileSizeMb { get; set; } = DefaultMaxFileSizeMb;

    /// <summary>
    /// Gets the largest hand-in request: every file at the largest size, and the form around them.
    /// </summary>
    public long MaxRequestBytes
        => (Math.Max(MaxFilesPerSubmission, 1) * (long)Math.Max(MaxFileSizeMb, 1) * 1024 * 1024) + FormOverheadBytes;
}
