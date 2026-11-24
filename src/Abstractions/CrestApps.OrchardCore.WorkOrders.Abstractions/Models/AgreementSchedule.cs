namespace CrestApps.OrchardCore.WorkOrders.Models;

/// <summary>
/// A recurring visit of a service agreement, such as a boiler service every six months.
/// </summary>
public sealed class AgreementSchedule
{
    /// <summary>
    /// Gets or sets the schedule's id, part of each work order's source.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets its name, such as Annual inspection.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the type of the work orders it makes.
    /// </summary>
    public string TypeId { get; set; }

    /// <summary>
    /// Gets or sets when it repeats, as an RFC 5545 rule such as <c>FREQ=MONTHLY;INTERVAL=6</c>, from the agreement's
    /// start date.
    /// </summary>
    public string Rule { get; set; }

    /// <summary>
    /// Gets or sets how many days ahead of a visit its work order is made.
    /// </summary>
    public int LeadDays { get; set; } = 14;

    /// <summary>
    /// Gets or sets the assets it covers; empty for every asset of the agreement.
    /// </summary>
    public List<string> AssetIds { get; set; } = [];
}
