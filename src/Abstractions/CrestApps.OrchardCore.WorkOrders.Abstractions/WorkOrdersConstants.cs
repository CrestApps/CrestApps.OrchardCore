namespace CrestApps.OrchardCore.WorkOrders;

/// <summary>
/// The constants of the Work Orders module.
/// </summary>
public static class WorkOrdersConstants
{
    /// <summary>
    /// The YesSql collection work orders are stored in.
    /// </summary>
    public const string CollectionName = "WorkOrders";

    /// <summary>
    /// The reference type of stock movements and invoices made for work orders.
    /// </summary>
    public const string ReferenceType = "WorkOrder";

    /// <summary>
    /// The prefix of work order numbers.
    /// </summary>
    public const string NumberPrefix = "WO-";

    /// <summary>
    /// The feature identifiers.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// Work orders.
        /// </summary>
        public const string WorkOrders = "CrestApps.OrchardCore.WorkOrders";
    }

    /// <summary>
    /// The workflow events.
    /// </summary>
    public static class Events
    {
        /// <summary>
        /// A work order was created.
        /// </summary>
        public const string Created = "WorkOrderCreated";

        /// <summary>
        /// A work order's status changed.
        /// </summary>
        public const string StatusChanged = "WorkOrderStatusChanged";

        /// <summary>
        /// A work order is close to missing its response or resolution target.
        /// </summary>
        public const string SlaWarning = "WorkOrderSlaWarning";

        /// <summary>
        /// A work order missed its response or resolution target.
        /// </summary>
        public const string SlaBreached = "WorkOrderSlaBreached";

        /// <summary>
        /// A work order was completed.
        /// </summary>
        public const string Completed = "WorkOrderCompleted";
    }
}
