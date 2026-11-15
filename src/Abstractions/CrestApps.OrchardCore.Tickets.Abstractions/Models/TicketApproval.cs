using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// A ticket's approval: the steps it waits for, one after the other, before work goes on. Each step is approved when
/// one of its approvers approves it; one refusal refuses the request.
/// </summary>
public sealed class TicketApproval
{
    /// <summary>
    /// Gets or sets where the approval stands.
    /// </summary>
    public TicketApprovalState State { get; set; }

    /// <summary>
    /// Gets or sets the steps, in order.
    /// </summary>
    public List<TicketApprovalStep> Steps { get; set; } = [];

    /// <summary>
    /// Gets or sets the index of the step waited for while <see cref="State"/> is <see cref="TicketApprovalState.Pending"/>.
    /// </summary>
    public int CurrentStep { get; set; }

    /// <summary>
    /// Gets or sets the status the ticket had when it started waiting, which it goes back to once approved.
    /// </summary>
    public string HeldStatusId { get; set; }

    /// <summary>
    /// Gets the step waited for, or <see langword="null"/> when nothing is waited for.
    /// </summary>
    [JsonIgnore]
    public TicketApprovalStep Current
        => State == TicketApprovalState.Pending && CurrentStep >= 0 && CurrentStep < Steps.Count ? Steps[CurrentStep] : null;
}

/// <summary>
/// One step of a ticket's approval.
/// </summary>
public sealed class TicketApprovalStep
{
    /// <summary>
    /// Gets or sets the step's name, such as "Manager" or "Finance".
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the users who may decide the step.
    /// </summary>
    public List<string> ApproverIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the decision, once made.
    /// </summary>
    public TicketApprovalDecision? Decision { get; set; }

    /// <summary>
    /// Gets or sets who decided.
    /// </summary>
    public string DecidedById { get; set; }

    /// <summary>
    /// Gets or sets the name of who decided.
    /// </summary>
    public string DecidedByName { get; set; }

    /// <summary>
    /// Gets or sets when it was decided.
    /// </summary>
    public DateTime? DecidedUtc { get; set; }

    /// <summary>
    /// Gets or sets the approver's comment.
    /// </summary>
    public string Comment { get; set; }
}

/// <summary>
/// Where a ticket's approval stands.
/// </summary>
public enum TicketApprovalState
{
    /// <summary>
    /// Waiting for a step.
    /// </summary>
    Pending,

    /// <summary>
    /// Every step approved.
    /// </summary>
    Approved,

    /// <summary>
    /// A step refused.
    /// </summary>
    Rejected,
}

/// <summary>
/// An approver's decision.
/// </summary>
public enum TicketApprovalDecision
{
    /// <summary>
    /// Approved.
    /// </summary>
    Approved,

    /// <summary>
    /// Refused.
    /// </summary>
    Rejected,
}

/// <summary>
/// The approval steps of a ticket type, in the Ticket part's settings on the type.
/// </summary>
public sealed class TicketApprovalSettings
{
    /// <summary>
    /// Gets or sets the steps a new ticket of the type waits for, in order; none means no approval.
    /// </summary>
    public List<TicketApprovalStepDefinition> Steps { get; set; } = [];

    /// <summary>
    /// Gets or sets the status a ticket waits in, such as "Waiting for approval", or <see langword="null"/> for the
    /// first waiting status.
    /// </summary>
    public string WaitingStatusId { get; set; }
}

/// <summary>
/// One approval step of a ticket type.
/// </summary>
public sealed class TicketApprovalStepDefinition
{
    /// <summary>
    /// Gets or sets the step's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the users who may decide it.
    /// </summary>
    public List<string> ApproverIds { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the requester's manager (from their staff record) may decide it too.
    /// </summary>
    public bool RequesterManager { get; set; }
}
