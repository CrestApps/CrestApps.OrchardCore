using CrestApps.OrchardCore.ContentFields.Taxonomies;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Categories;

/// <summary>
/// What the ticket forms (the agents' New ticket page, the portal, the web form) need of a ticket type's categories: the
/// choices for the cascading picker, and the chosen category put on the new item before its editor is read, so the
/// type's field rules on the category see it.
/// </summary>
internal static class TicketCategoryForm
{
    /// <summary>
    /// The choices of the category picker.
    /// </summary>
    /// <param name="categories">The categories offered, in tree order.</param>
    /// <param name="leavesOnly">Whether only a category without sub-categories may be chosen.</param>
    /// <returns>The choices.</returns>
    public static List<CascadingPickerItem> Items(IEnumerable<TicketCategory> categories, bool leavesOnly)
    {
        var offered = (categories ?? []).ToList();
        var ids = offered.Select(category => category.Id).ToHashSet(StringComparer.Ordinal);

        // A category whose parent isn't offered (hidden from customers) can't be reached one level at a time; it is
        // left out with it.
        return [.. offered
            .Where(category => category.ParentId is null || ids.Contains(category.ParentId))
            .Select(category => new CascadingPickerItem(
                category.Id,
                category.Name,
                category.ParentId,
                Selectable: !leavesOnly || !offered.Any(child => child.ParentId == category.Id),
                Description: category.CustomerDescription))];
    }

    /// <summary>
    /// Finds the chosen category among those offered and puts it on the new ticket.
    /// </summary>
    /// <param name="categories">The category service.</param>
    /// <param name="ticket">The new ticket, before its editor is read.</param>
    /// <param name="categoryId">The posted category.</param>
    /// <param name="customersOnly">Whether only categories customers can choose are accepted.</param>
    /// <returns>The category, or <see langword="null"/> when none (or one that isn't offered) was chosen.</returns>
    public static async Task<TicketCategory> ApplyAsync(ITicketCategoryService categories, ContentItem ticket, string categoryId, bool customersOnly)
    {
        if (categories is null || string.IsNullOrWhiteSpace(categoryId))
        {
            return null;
        }

        var category = (await categories.ListAsync(ticket.ContentType, customersOnly)).FirstOrDefault(item => item.Id == categoryId.Trim());

        if (category is not null)
        {
            ticket.Alter<TicketPart>(part =>
            {
                part.CategoryId = category.Id;
                part.CategoryPath = category.Path;
            });
        }

        return category;
    }
}
