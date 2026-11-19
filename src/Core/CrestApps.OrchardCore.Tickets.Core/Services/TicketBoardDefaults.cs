using CrestApps.OrchardCore.Tickets.Models;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// The columns a new board starts with: one per status category that has statuses, closed tickets left out, each column
/// with the category's statuses. A column with several statuses asks which one when a card is dropped on it.
/// </summary>
public static class TicketBoardDefaults
{
    private static readonly TicketStatusCategory[] _categories =
    [
        TicketStatusCategory.New,
        TicketStatusCategory.Open,
        TicketStatusCategory.Pending,
        TicketStatusCategory.OnHold,
        TicketStatusCategory.Solved,
    ];

    /// <summary>
    /// Returns the columns a new board starts with.
    /// </summary>
    /// <param name="statuses">The statuses.</param>
    /// <param name="nameOf">The name of a category's column.</param>
    /// <returns>The columns, from left to right.</returns>
    public static List<TicketBoardColumn> Columns(IEnumerable<TicketStatus> statuses, Func<TicketStatusCategory, string> nameOf)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        ArgumentNullException.ThrowIfNull(nameOf);

        var ordered = statuses.OrderBy(status => status.Order).ThenBy(status => status.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var columns = new List<TicketBoardColumn>();

        foreach (var category in _categories)
        {
            var ids = ordered.Where(status => status.Category == category).Select(status => status.ItemId).ToList();

            if (ids.Count > 0)
            {
                columns.Add(new TicketBoardColumn
                {
                    Id = category.ToString().ToLowerInvariant(),
                    Name = nameOf(category),
                    StatusIds = ids,
                });
            }
        }

        return columns;
    }

    /// <summary>
    /// Returns the English name of a category's column, for boards made without a request, such as when the feature is
    /// enabled.
    /// </summary>
    /// <param name="category">The category.</param>
    /// <returns>The name.</returns>
    public static string EnglishName(TicketStatusCategory category)
        => category switch
        {
            TicketStatusCategory.New => "New",
            TicketStatusCategory.Open => "Open",
            TicketStatusCategory.Pending => "Pending",
            TicketStatusCategory.OnHold => "On hold",
            TicketStatusCategory.Solved => "Solved",
            _ => "Closed",
        };
}
