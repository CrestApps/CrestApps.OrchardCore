namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// An entry of a document's history, which is append-only: what happened, who did it and when.
/// </summary>
public sealed class DocumentEvent
{
    /// <summary>
    /// Gets or sets the entry's id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets what happened, one of <see cref="DocumentEventKinds"/> or a kind another module adds.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it happened.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the id of the user who did it, or <see langword="null"/> for the system.
    /// </summary>
    public string ActorId { get; set; }

    /// <summary>
    /// Gets or sets the name of the user who did it, as it was then.
    /// </summary>
    public string ActorName { get; set; }

    /// <summary>
    /// Gets or sets a description of what happened.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets an amount the entry is about, such as a payment.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Gets or sets the kind of a record the entry names, such as a credit note or a transaction entry.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the id of the record the entry names.
    /// </summary>
    public string ReferenceId { get; set; }
}
