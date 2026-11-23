using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// One attempt to file a document electronically: what was filed (the target), in which file format, through which
/// filing channel (transport), and what the agency answered. A document may have several, such as one rejected and one
/// accepted.
/// </summary>
public sealed class EFileSubmission : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the kind of document filed, such as <c>TaxReturn</c>.
    /// </summary>
    public string TargetType { get; set; }

    /// <summary>
    /// Gets or sets the id of the document filed.
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Gets or sets what people see of the document, as it was when it was submitted.
    /// </summary>
    public string TargetDisplayText { get; set; }

    /// <summary>
    /// Gets or sets the name of the file format (<c>IEFileFormat</c>).
    /// </summary>
    public string Format { get; set; }

    /// <summary>
    /// Gets or sets the name of the filing channel (<c>IEFileTransport</c>).
    /// </summary>
    public string Transport { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public EFileSubmissionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the channel is manual: a person files the file and records the answer,
    /// so the channel is never asked for it.
    /// </summary>
    public bool IsManual { get; set; }

    /// <summary>
    /// Gets or sets the name of the file built.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the media type of the file built.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the file built, encoded as base64, kept so what was sent can be downloaded again.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the id the agency or the channel gave the submission, used to ask for its acknowledgement.
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// Gets or sets the confirmation number the agency gave once it accepted the document.
    /// </summary>
    public string ConfirmationNumber { get; set; }

    /// <summary>
    /// Gets or sets the messages of the channel and the agency, such as the reasons of a rejection.
    /// </summary>
    public IList<string> Messages { get; set; } = [];

    /// <summary>
    /// Gets or sets who submitted it.
    /// </summary>
    public string SubmittedBy { get; set; }

    /// <summary>
    /// Gets or sets when it was submitted.
    /// </summary>
    public DateTime? SubmittedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the agency's answer was recorded.
    /// </summary>
    public DateTime? AcknowledgedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the channel was last asked for the answer.
    /// </summary>
    public DateTime? LastCheckedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the submission was added.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <inheritdoc />
    public DateTime? ModifiedUtc { get; set; }
}

/// <summary>
/// Where a submission is.
/// </summary>
public enum EFileSubmissionStatus
{
    /// <summary>
    /// The file was built; nothing was sent yet.
    /// </summary>
    Created,

    /// <summary>
    /// The file was handed over: sent to the agency and waiting for its answer, or downloaded to file by hand and
    /// waiting for the confirmation number to be recorded.
    /// </summary>
    Submitted,

    /// <summary>
    /// The agency accepted the document; the document is marked as filed.
    /// </summary>
    Accepted,

    /// <summary>
    /// The agency refused the document; correct it and submit again.
    /// </summary>
    Rejected,

    /// <summary>
    /// The channel could not send the file; nothing reached the agency.
    /// </summary>
    Failed,
}

/// <summary>
/// A document that can be filed electronically, as its target provider describes it.
/// </summary>
public sealed class EFileTarget
{
    /// <summary>
    /// Gets or sets the kind of document, such as <c>TaxReturn</c>.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the document's id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets what people see of the document.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the jurisdiction it is filed with.
    /// </summary>
    public string JurisdictionId { get; set; }

    /// <summary>
    /// Gets or sets the form it is on, so a format offers only the forms it can write.
    /// </summary>
    public string Form { get; set; }

    /// <summary>
    /// Gets or sets why it can't be filed now, such as a return that is still a draft, or <see langword="null"/> when it
    /// can.
    /// </summary>
    public string NotFileableReason { get; set; }

    /// <summary>
    /// Gets or sets the document itself, such as the <see cref="TaxReturn"/>.
    /// </summary>
    public object Document { get; set; }
}

/// <summary>
/// The file a format built.
/// </summary>
public sealed class EFilePayload
{
    /// <summary>
    /// Gets or sets the file name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the media type.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the bytes.
    /// </summary>
    public byte[] Content { get; set; } = [];

    /// <summary>
    /// Gets the problems that keep the file from being sent, such as a missing registration number.
    /// </summary>
    public IList<string> Problems { get; } = [];
}

/// <summary>
/// What a filing channel did.
/// </summary>
public sealed class EFileTransportResult
{
    /// <summary>
    /// Gets or sets the status of the submission after the call.
    /// </summary>
    public EFileSubmissionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the id the agency or the channel gave the submission, or <see langword="null"/>.
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// Gets or sets the confirmation number of an accepted document, or <see langword="null"/>.
    /// </summary>
    public string ConfirmationNumber { get; set; }

    /// <summary>
    /// Gets or sets the day the agency counts the document as filed, or <see langword="null"/> for the day it was
    /// accepted.
    /// </summary>
    public DateOnly? FiledOn { get; set; }

    /// <summary>
    /// Gets the messages of the channel and the agency.
    /// </summary>
    public IList<string> Messages { get; } = [];
}
