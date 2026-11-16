using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Tickets.Reports.DataSources;

/// <summary>
/// The names of the Tickets data source and its data sets.
/// </summary>
public static class TicketReportDataSets
{
    public const string Source = "Tickets";

    public const string Tickets = "Tickets";

    public const string TicketEntries = "TicketEntries";

    public const string TicketChanges = "TicketChanges";

    public const string TicketLinks = "TicketLinks";

    public const string TicketBacklog = "TicketBacklog";

    /// <summary>The ticket categories data set.</summary>
    public const string TicketCategories = "TicketCategories";

    /// <summary>The field of a category's id, which tickets and the backlog join on.</summary>
    public const string CategoryIdField = "CategoryId";

    /// <summary>
    /// A reference to a ticket category.
    /// </summary>
    /// <returns>The reference.</returns>
    public static DataFieldReference Category()
        => new(Source, TicketCategories, CategoryIdField);

    /// <summary>
    /// The field that holds a ticket's content item id in every data set.
    /// </summary>
    public const string TicketIdField = "TicketId";

    /// <summary>
    /// Gets a reference to a ticket in the Tickets data set.
    /// </summary>
    /// <returns>The reference.</returns>
    public static DataFieldReference Ticket()
        => new(Source, Tickets, TicketIdField);
}
