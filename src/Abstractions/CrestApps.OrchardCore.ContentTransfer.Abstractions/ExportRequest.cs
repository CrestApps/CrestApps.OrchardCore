using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentTransfer;

/// <summary>
/// The display-driven model for the bulk export form. Modules contribute additional export option shapes
/// by registering an <c>IDisplayDriver&lt;ExportRequest&gt;</c>; on submit they read their values back and
/// persist them as parts on <see cref="Entry"/> so the export background task and the content import
/// handlers can honor them.
/// </summary>
public sealed class ExportRequest
{
    /// <summary>
    /// Gets or sets the content type the export targets. This is empty while the export form is first
    /// rendered (the user has not chosen a type yet) and set when the export is submitted.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the definition of <see cref="ContentType"/> when it is known.
    /// </summary>
    public ContentTypeDefinition ContentTypeDefinition { get; set; }

    /// <summary>
    /// Gets or sets the entry that carries the queued export. Drivers store their contributed options here
    /// as parts. It is <see langword="null"/> while the form is first rendered.
    /// </summary>
    public ContentTransferEntry Entry { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the export must run through the queued/background path. A
    /// driver sets this when its options require the persisted entry (for example, to be read back by an
    /// export handler), even when the record count is below the immediate-export threshold.
    /// </summary>
    public bool RequiresQueue { get; set; }
}
