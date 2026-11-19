using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Attachments.Models;

/// <summary>
/// A file to attach to a record.
/// </summary>
public sealed class AttachmentUpload
{
    /// <summary>
    /// Gets or sets the record to attach it to.
    /// </summary>
    public RecordRef Owner { get; set; }

    /// <summary>
    /// Gets or sets the file's name as the sender gave it.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the media type the sender declared; the file's own bytes decide.
    /// </summary>
    public string DeclaredContentType { get; set; }

    /// <summary>
    /// Gets or sets the file's bytes.
    /// </summary>
    public ReadOnlyMemory<byte> Content { get; set; }

    /// <summary>
    /// Gets or sets who uploads it.
    /// </summary>
    public PartyRef UploadedBy { get; set; }
}
