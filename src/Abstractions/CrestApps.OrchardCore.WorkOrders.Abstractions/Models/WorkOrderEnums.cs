namespace CrestApps.OrchardCore.WorkOrders.Models;

/// <summary>
/// How urgent a work order is.
/// </summary>
public enum WorkOrderPriority
{
    /// <summary>
    /// Low.
    /// </summary>
    Low,

    /// <summary>
    /// Normal.
    /// </summary>
    Normal,

    /// <summary>
    /// High.
    /// </summary>
    High,

    /// <summary>
    /// Urgent.
    /// </summary>
    Urgent,
}

/// <summary>
/// Where a work order is in its life.
/// </summary>
public enum WorkOrderStatus
{
    /// <summary>
    /// Just created.
    /// </summary>
    New,

    /// <summary>
    /// Ready to schedule.
    /// </summary>
    Ready,

    /// <summary>
    /// Booked with a technician.
    /// </summary>
    Scheduled,

    /// <summary>
    /// Being worked on.
    /// </summary>
    InProgress,

    /// <summary>
    /// Waiting, such as for the customer or a part.
    /// </summary>
    OnHold,

    /// <summary>
    /// The work is done.
    /// </summary>
    Completed,

    /// <summary>
    /// Done and closed, such as once invoiced.
    /// </summary>
    Closed,

    /// <summary>
    /// Canceled.
    /// </summary>
    Canceled,
}

/// <summary>
/// What a work order line is.
/// </summary>
public enum WorkOrderLineKind
{
    /// <summary>
    /// A part or product.
    /// </summary>
    Product,

    /// <summary>
    /// A service.
    /// </summary>
    Service,

    /// <summary>
    /// Labor, by the hour.
    /// </summary>
    Labor,

    /// <summary>
    /// An expense, such as a parking fee.
    /// </summary>
    Expense,
}

/// <summary>
/// How a work order is billed.
/// </summary>
public enum WorkOrderBillingMode
{
    /// <summary>
    /// Not billed.
    /// </summary>
    NotBillable,

    /// <summary>
    /// A fixed price.
    /// </summary>
    FixedPrice,

    /// <summary>
    /// Time and materials: its billable lines.
    /// </summary>
    TimeAndMaterials,
}

/// <summary>
/// Where a work order came from.
/// </summary>
public enum WorkOrderSource
{
    /// <summary>
    /// Entered by hand.
    /// </summary>
    Manual,

    /// <summary>
    /// A booked appointment.
    /// </summary>
    Appointment,

    /// <summary>
    /// A service agreement.
    /// </summary>
    Agreement,

    /// <summary>
    /// A contact center activity.
    /// </summary>
    Activity,

    /// <summary>
    /// The API.
    /// </summary>
    Api,

    /// <summary>
    /// An AI assistant.
    /// </summary>
    AI,

    /// <summary>
    /// A workflow.
    /// </summary>
    Workflow,
}

/// <summary>
/// The kind of answer a checklist question takes.
/// </summary>
public enum ChecklistQuestionKind
{
    /// <summary>
    /// Yes or no.
    /// </summary>
    YesNo,

    /// <summary>
    /// Pass, fail or not applicable.
    /// </summary>
    PassFailNA,

    /// <summary>
    /// Text.
    /// </summary>
    Text,

    /// <summary>
    /// A number, within a range.
    /// </summary>
    Number,

    /// <summary>
    /// One of a list.
    /// </summary>
    Choice,

    /// <summary>
    /// Some of a list.
    /// </summary>
    MultiChoice,

    /// <summary>
    /// Photos.
    /// </summary>
    Photo,

    /// <summary>
    /// A signature.
    /// </summary>
    Signature,

    /// <summary>
    /// A reading of an asset's meter.
    /// </summary>
    MeterReading,

    /// <summary>
    /// A date.
    /// </summary>
    Date,
}
