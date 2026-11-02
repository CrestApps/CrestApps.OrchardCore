using CrestApps.Core.Models;
using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.WorkOrders.Models;

/// <summary>
/// A part, service, labor or expense on a work order.
/// </summary>
public sealed class WorkOrderLine
{
    /// <summary>
    /// Gets or sets the line identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets what it is.
    /// </summary>
    public WorkOrderLineKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the product, for a product line.
    /// </summary>
    public string ProductId { get; set; }

    /// <summary>
    /// Gets or sets the Inventory stock item, for a counted part.
    /// </summary>
    public string StockItemId { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets how many were expected.
    /// </summary>
    public decimal QuantityEstimated { get; set; }

    /// <summary>
    /// Gets or sets how many were used.
    /// </summary>
    public decimal QuantityUsed { get; set; }

    /// <summary>
    /// Gets or sets the price of one.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the Inventory location parts were used from.
    /// </summary>
    public string LocationId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether it is billed.
    /// </summary>
    public bool Billable { get; set; } = true;
}

/// <summary>
/// The person to talk to about a work order.
/// </summary>
public sealed class WorkOrderContact
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the phone number.
    /// </summary>
    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets the email address.
    /// </summary>
    public string Email { get; set; }
}

/// <summary>
/// A work order's service level: its targets and how far it is from them.
/// </summary>
public sealed class WorkOrderSla
{
    /// <summary>
    /// Gets or sets the policy.
    /// </summary>
    public string PolicyId { get; set; }

    /// <summary>
    /// Gets or sets when the first response is due.
    /// </summary>
    public DateTime? RespondByUtc { get; set; }

    /// <summary>
    /// Gets or sets when it must be resolved.
    /// </summary>
    public DateTime? ResolveByUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was first responded to.
    /// </summary>
    public DateTime? FirstResponseUtc { get; set; }

    /// <summary>
    /// Gets or sets when the clock stopped while it waits, or <see langword="null"/> while it runs.
    /// </summary>
    public DateTime? PausedSinceUtc { get; set; }

    /// <summary>
    /// Gets or sets how long the clock has stopped, in seconds.
    /// </summary>
    public long PausedSeconds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the response warning was raised.
    /// </summary>
    public bool ResponseWarned { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the response target was missed.
    /// </summary>
    public bool ResponseBreached { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the resolution warning was raised.
    /// </summary>
    public bool ResolutionWarned { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the resolution target was missed.
    /// </summary>
    public bool ResolutionBreached { get; set; }
}

/// <summary>
/// An answer to a checklist question.
/// </summary>
public sealed class ChecklistAnswer
{
    /// <summary>
    /// Gets or sets the question.
    /// </summary>
    public string QuestionId { get; set; }

    /// <summary>
    /// Gets or sets the answer, as text; several answers are separated by new lines.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets when it was answered.
    /// </summary>
    public DateTime Utc { get; set; }

    /// <summary>
    /// Gets or sets who answered.
    /// </summary>
    public string By { get; set; }
}

/// <summary>
/// A checklist on a work order, with its answers.
/// </summary>
public sealed class WorkOrderChecklist
{
    /// <summary>
    /// Gets or sets the template it came from.
    /// </summary>
    public string TemplateId { get; set; }

    /// <summary>
    /// Gets or sets its name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the template's questions when it was added.
    /// </summary>
    public List<ChecklistQuestion> Questions { get; set; } = [];

    /// <summary>
    /// Gets or sets the answers.
    /// </summary>
    public List<ChecklistAnswer> Answers { get; set; } = [];
}

/// <summary>
/// A signature on a work order.
/// </summary>
public sealed class WorkOrderSignature
{
    /// <summary>
    /// Gets or sets who signed.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the path of the signature image, when drawn.
    /// </summary>
    public string ImagePath { get; set; }

    /// <summary>
    /// Gets or sets when.
    /// </summary>
    public DateTime Utc { get; set; }

    /// <summary>
    /// Gets or sets the visit it was signed on.
    /// </summary>
    public string VisitId { get; set; }
}

/// <summary>
/// A note on a work order.
/// </summary>
public sealed class WorkOrderNote
{
    /// <summary>
    /// Gets or sets when.
    /// </summary>
    public DateTime Utc { get; set; }

    /// <summary>
    /// Gets or sets who.
    /// </summary>
    public string By { get; set; }

    /// <summary>
    /// Gets or sets the text.
    /// </summary>
    public string Text { get; set; }
}

/// <summary>
/// A job to do for a customer: what, where, when, with what, and how it went.
/// </summary>
public sealed class WorkOrder : CatalogItem
{
    /// <summary>
    /// Gets or sets the number, such as WO-000042.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string TypeId { get; set; }

    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    public WorkOrderPriority Priority { get; set; } = WorkOrderPriority.Normal;

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public WorkOrderStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the sub-status.
    /// </summary>
    public string SubStatusId { get; set; }

    /// <summary>
    /// Gets or sets the summary.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the customer.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the company customer.
    /// </summary>
    public string CompanyId { get; set; }

    /// <summary>
    /// Gets or sets the CRM contact.
    /// </summary>
    public string ContactId { get; set; }

    /// <summary>
    /// Gets or sets the person to talk to.
    /// </summary>
    public WorkOrderContact Contact { get; set; } = new();

    /// <summary>
    /// Gets or sets where the work is, with coordinates once geocoded.
    /// </summary>
    public Address ServiceAddress { get; set; }

    /// <summary>
    /// Gets or sets how to get in, such as a gate code.
    /// </summary>
    public string AccessNotes { get; set; }

    /// <summary>
    /// Gets or sets the territory of the service address.
    /// </summary>
    public string TerritoryId { get; set; }

    /// <summary>
    /// Gets or sets the assets worked on.
    /// </summary>
    public List<string> AssetIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the earliest the customer wants the visit.
    /// </summary>
    public DateTime? EarliestUtc { get; set; }

    /// <summary>
    /// Gets or sets the latest the customer wants the visit.
    /// </summary>
    public DateTime? LatestUtc { get; set; }

    /// <summary>
    /// Gets or sets the service level.
    /// </summary>
    public WorkOrderSla Sla { get; set; } = new();

    /// <summary>
    /// Gets or sets the skills it needs.
    /// </summary>
    public List<RequiredSkill> RequiredSkills { get; set; } = [];

    /// <summary>
    /// Gets or sets how long it should take, in minutes.
    /// </summary>
    public int EstimatedMinutes { get; set; }

    /// <summary>
    /// Gets or sets the workers the customer would like.
    /// </summary>
    public List<string> PreferredWorkerIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the workers who mustn't be sent.
    /// </summary>
    public List<string> ExcludedWorkerIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the lines.
    /// </summary>
    public List<WorkOrderLine> Lines { get; set; } = [];

    /// <summary>
    /// Gets or sets the checklists.
    /// </summary>
    public List<WorkOrderChecklist> Checklists { get; set; } = [];

    /// <summary>
    /// Gets or sets the paths of its photos and files in the media library.
    /// </summary>
    public List<string> Attachments { get; set; } = [];

    /// <summary>
    /// Gets or sets the signatures.
    /// </summary>
    public List<WorkOrderSignature> Signatures { get; set; } = [];

    /// <summary>
    /// Gets or sets the notes, oldest first.
    /// </summary>
    public List<WorkOrderNote> Notes { get; set; } = [];

    /// <summary>
    /// Gets or sets where it came from.
    /// </summary>
    public WorkOrderSource Source { get; set; }

    /// <summary>
    /// Gets or sets the record it came from, such as an appointment.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets the work order it follows up.
    /// </summary>
    public string ParentWorkOrderId { get; set; }

    /// <summary>
    /// Gets or sets its invoice.
    /// </summary>
    public string InvoiceTransactionId { get; set; }

    /// <summary>
    /// Gets or sets the service agreement it was made for, if any.
    /// </summary>
    public string AgreementId { get; set; }

    /// <summary>
    /// Gets or sets the price agreed for it, which replaces its type's billing: zero when an agreement covers it, or
    /// <see langword="null"/> to bill as its type says.
    /// </summary>
    public decimal? AgreedPrice { get; set; }

    /// <summary>
    /// Gets or sets why it was canceled.
    /// </summary>
    public string CancelReason { get; set; }

    /// <summary>
    /// Gets or sets when it was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was completed.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was closed.
    /// </summary>
    public DateTime? ClosedUtc { get; set; }

    /// <summary>
    /// Gets or sets who closed it.
    /// </summary>
    public string ClosedBy { get; set; }
}
