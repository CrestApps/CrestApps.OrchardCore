namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// A ticket category as its tickets see it: the term, the categories above it and the routing it gives, each value taken
/// from the term or the nearest category above it that sets it.
/// </summary>
public sealed class TicketCategory
{
    /// <summary>
    /// Gets or sets the term's content item id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the term's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the category above it, or <see langword="null"/> at the top.
    /// </summary>
    public string ParentId { get; set; }

    /// <summary>
    /// Gets or sets its depth, 0 at the top.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Gets or sets the names from the top down to it, such as "Hardware > Laptop".
    /// </summary>
    public string PathText { get; set; }

    /// <summary>
    /// Gets or sets its id and those above it, top first, as <see cref="TicketPart.CategoryPath"/> keeps them.
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// Gets or sets whether it has sub-categories.
    /// </summary>
    public bool HasChildren { get; set; }

    /// <summary>
    /// Gets or sets the desk its new tickets go to, if any.
    /// </summary>
    public string DeskId { get; set; }

    /// <summary>
    /// Gets or sets the team its new tickets go to, if any.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the priority its new tickets start with, if any.
    /// </summary>
    public string PriorityId { get; set; }

    /// <summary>
    /// Gets or sets the service-level policy of its tickets, if any.
    /// </summary>
    public string ServiceLevelPolicyId { get; set; }

    /// <summary>
    /// Gets or sets whether customers can choose it.
    /// </summary>
    public bool CustomersCanChoose { get; set; }

    /// <summary>
    /// Gets or sets what customers read about it.
    /// </summary>
    public string CustomerDescription { get; set; }

    /// <summary>
    /// Makes the path kept on a ticket from a category's id and the ids above it.
    /// </summary>
    /// <param name="id">The category.</param>
    /// <param name="ancestorIds">The categories above it, parent first.</param>
    /// <returns>The path, such as <c>/top/child/category/</c>.</returns>
    public static string MakePath(string id, IEnumerable<string> ancestorIds)
        => "/" + string.Join('/', (ancestorIds ?? []).Reverse().Append(id)) + "/";
}
