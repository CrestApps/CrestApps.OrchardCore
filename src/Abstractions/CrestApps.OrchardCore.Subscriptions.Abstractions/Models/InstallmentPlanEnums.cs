namespace CrestApps.OrchardCore.Subscriptions.Models;

/// <summary>
/// Where an <see cref="InstallmentPlan"/> stands.
/// </summary>
public enum InstallmentPlanStatus
{
    /// <summary>
    /// Created, waiting for the down payment. Nothing is scheduled until it is received.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// The down payment was received and the scheduled payments are being collected.
    /// </summary>
    Active = 1,

    /// <summary>
    /// A payment is overdue or could not be charged.
    /// </summary>
    PastDue = 2,

    /// <summary>
    /// Every payment was received.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Canceled; payments not yet received are no longer collected.
    /// </summary>
    Canceled = 4,
}

/// <summary>
/// Where one payment on an installment plan stands.
/// </summary>
public enum InstallmentPaymentStatus
{
    /// <summary>
    /// Not yet due.
    /// </summary>
    Scheduled = 0,

    /// <summary>
    /// Due and waiting to be paid by the customer, or to be charged.
    /// </summary>
    Due = 1,

    /// <summary>
    /// Received.
    /// </summary>
    Paid = 2,

    /// <summary>
    /// Charging the saved payment method failed; it is retried, or left for the customer to pay.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// No longer collected because the plan was canceled.
    /// </summary>
    Canceled = 4,
}

/// <summary>
/// How often the payments after the down payment fall due.
/// </summary>
public enum InstallmentFrequency
{
    /// <summary>
    /// Every week.
    /// </summary>
    Weekly = 0,

    /// <summary>
    /// Every two weeks.
    /// </summary>
    BiWeekly = 1,

    /// <summary>
    /// Every month, on the same day of the month as the first payment (or the last day of a shorter month).
    /// </summary>
    Monthly = 2,

    /// <summary>
    /// Every three months.
    /// </summary>
    Quarterly = 3,
}

/// <summary>
/// How the payments after the down payment are collected.
/// </summary>
public enum InstallmentCollectionMethod
{
    /// <summary>
    /// The payment method saved with the down payment is charged on each due date.
    /// </summary>
    AutoCharge = 0,

    /// <summary>
    /// The customer is reminded and pays each one from their transactions.
    /// </summary>
    Invoice = 1,
}

/// <summary>
/// A kind of entry in an installment plan's history.
/// </summary>
public enum InstallmentPlanEventType
{
    /// <summary>
    /// The plan was created.
    /// </summary>
    Created = 0,

    /// <summary>
    /// The down payment was received and the schedule started.
    /// </summary>
    Activated = 1,

    /// <summary>
    /// A payment was received.
    /// </summary>
    PaymentReceived = 2,

    /// <summary>
    /// Charging the saved payment method failed.
    /// </summary>
    ChargeFailed = 3,

    /// <summary>
    /// A payment became due and was left for the customer to pay.
    /// </summary>
    PaymentDue = 4,

    /// <summary>
    /// Every payment was received.
    /// </summary>
    Completed = 5,

    /// <summary>
    /// The plan was canceled.
    /// </summary>
    Canceled = 6,

    /// <summary>
    /// A note.
    /// </summary>
    Note = 7,
}
