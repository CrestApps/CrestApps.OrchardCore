using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Outreach.Models;

/// <summary>
/// One bulk send: who it goes to (an audience source), how (a channel sender), what (a template and its renderer), and
/// how far it has got. The engine sends it in batches on the leased work queue, one page of the audience at a time.
/// </summary>
/// <remarks>
/// The record the run sends for (a broadcast, a campaign) is named by <see cref="OwnerType"/> and <see cref="OwnerId"/>;
/// the sources, renderers and observers of that record read it through them.
/// </remarks>
public sealed class OutreachRun : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets a name for logs and lists.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the kind of record the run sends for, such as <c>MessagingBroadcast</c>.
    /// </summary>
    public string OwnerType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the record the run sends for.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the name of the <see cref="IOutreachAudienceSource"/> that pages the recipients.
    /// </summary>
    public string AudienceSource { get; set; }

    /// <summary>
    /// Gets or sets the name of the <see cref="IOutreachChannelSender"/> that sends the messages.
    /// </summary>
    public string Sender { get; set; }

    /// <summary>
    /// Gets or sets the channel within the sender, such as <c>SMS</c> for the messaging sender; optional.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the address the messages are sent from; optional.
    /// </summary>
    public string SenderAddress { get; set; }

    /// <summary>
    /// Gets or sets who the messages are sent as, such as the agent who composed a broadcast; optional.
    /// </summary>
    public string ActorId { get; set; }

    /// <summary>
    /// Gets or sets the name of the <see cref="IOutreachTemplateRenderer"/>, or <see langword="null"/> to send the
    /// template as it is.
    /// </summary>
    public string Renderer { get; set; }

    /// <summary>
    /// Gets or sets what the run sends.
    /// </summary>
    public OutreachTemplate Template { get; set; } = new();

    /// <summary>
    /// Gets or sets the consent channel the engine asks the consent service about before each send, such as
    /// <c>Email</c> or <c>SMS</c>; <see langword="null"/> when the run's sender checks consent itself. A recipient who
    /// opted out of it is skipped.
    /// </summary>
    public string ConsentChannel { get; set; }

    /// <summary>
    /// Gets or sets the kind of person the recipients' <see cref="OutreachRecipient.ContactId"/> names, such as
    /// <c>Customer</c> or <c>Contact</c>, for the consent check; optional.
    /// </summary>
    public string ConsentContactKind { get; set; }

    /// <summary>
    /// Gets or sets the business hours schedule the run may send in, for channels that observe quiet hours; optional.
    /// Outside it, batches wait for the schedule to open.
    /// </summary>
    public string SendingHoursScheduleId { get; set; }

    /// <summary>
    /// Gets or sets how many recipients a batch handles.
    /// </summary>
    public int PageSize { get; set; } = OutreachConstants.DefaultPageSize;

    /// <summary>
    /// Gets or sets the state of the run.
    /// </summary>
    public OutreachRunStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the audience cursor the next batch starts at, or <see langword="null"/> for the start.
    /// </summary>
    public string Cursor { get; set; }

    /// <summary>
    /// Gets or sets the number of the last batch whose outcome was added to the counters.
    /// </summary>
    public int BatchesCompleted { get; set; }

    /// <summary>
    /// Gets or sets how many messages were sent.
    /// </summary>
    public int Sent { get; set; }

    /// <summary>
    /// Gets or sets how many recipients were skipped: no address, opted out, no consent.
    /// </summary>
    public int Skipped { get; set; }

    /// <summary>
    /// Gets or sets how many sends failed.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Gets or sets the error that stopped the run, when it failed.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the run was started.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the first batch was sent.
    /// </summary>
    public DateTime? StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the run ended.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <inheritdoc/>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets whether the run has ended and sends nothing more.
    /// </summary>
    public bool IsFinished
        => Status is OutreachRunStatus.Completed or OutreachRunStatus.Canceled or OutreachRunStatus.Failed;
}
