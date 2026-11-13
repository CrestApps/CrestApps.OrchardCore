namespace CrestApps.OrchardCore.Attachments.Models;

/// <summary>
/// The result of attaching files to a record.
/// </summary>
public sealed class AttachmentResult
{
    /// <summary>
    /// Gets the stored attachments, in the order given; empty when refused.
    /// </summary>
    public IReadOnlyList<Attachment> Attachments { get; init; } = [];

    /// <summary>
    /// Gets why the files were refused; empty when they were stored. When one file is refused, none is stored.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Gets whether the files were stored.
    /// </summary>
    public bool Succeeded => Errors.Count == 0;
}
