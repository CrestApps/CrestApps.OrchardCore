using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.WorkOrders.Models;

/// <summary>
/// The field-service terms of an agreement, kept in the agreement's properties: its recurring visits, where they are,
/// who to contact, the assets covered and how visits are paid for. An agreement with schedules makes their work orders
/// ahead of each visit while it is active.
/// </summary>
public sealed class WorkOrderAgreementTerms
{
    /// <summary>
    /// Gets or sets the customer (an Orchard Core user), when the agreement is with a person rather than an account.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the person to contact.
    /// </summary>
    public WorkOrderContact Contact { get; set; } = new();

    /// <summary>
    /// Gets or sets where the visits are.
    /// </summary>
    public Address ServiceAddress { get; set; }

    /// <summary>
    /// Gets or sets how to get in.
    /// </summary>
    public string AccessNotes { get; set; }

    /// <summary>
    /// Gets or sets the assets it covers.
    /// </summary>
    public List<string> AssetIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the recurring visits.
    /// </summary>
    public List<AgreementSchedule> Schedules { get; set; } = [];

    /// <summary>
    /// Gets or sets how the visits are paid for.
    /// </summary>
    public AgreementPricing Pricing { get; set; }

    /// <summary>
    /// Gets or sets the price of a visit, when each is invoiced.
    /// </summary>
    public decimal PricePerVisit { get; set; }

    /// <summary>
    /// Gets whether the terms say anything, so an agreement with none keeps no field-service terms.
    /// </summary>
    public bool HasAny
        => !string.IsNullOrEmpty(CustomerId)
            || !string.IsNullOrEmpty(Contact?.Name) || !string.IsNullOrEmpty(Contact?.Phone) || !string.IsNullOrEmpty(Contact?.Email)
            || ServiceAddress is not null
            || !string.IsNullOrEmpty(AccessNotes)
            || AssetIds.Count > 0
            || Schedules.Count > 0
            || Pricing != AgreementPricing.Included;
}
