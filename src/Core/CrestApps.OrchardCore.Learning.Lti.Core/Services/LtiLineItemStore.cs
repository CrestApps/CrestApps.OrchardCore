using CrestApps.OrchardCore.Learning.Lti.Core.Indexes;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Services;

/// <summary>
/// Stores the line items of Assignment and Grade Services.
/// </summary>
public sealed class LtiLineItemStore : DocumentCatalog<LtiLineItem, LtiLineItemIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LtiLineItemStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public LtiLineItemStore(ISession session)
        : base(session)
    {
        CollectionName = LtiConstants.CollectionName;
    }

    /// <summary>
    /// Pages the line items of a course that belong to a tool, oldest first, filtered as the line items service allows.
    /// </summary>
    /// <param name="courseId">The course.</param>
    /// <param name="toolId">The tool.</param>
    /// <param name="resourceLinkId">Only this resource link, or <see langword="null"/>.</param>
    /// <param name="resourceId">Only this resource id, or <see langword="null"/>.</param>
    /// <param name="tag">Only this tag, or <see langword="null"/>.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The total and the page.</returns>
    public async Task<(int Count, IReadOnlyList<LtiLineItem> Items)> PageAsync(
        string courseId,
        string toolId,
        string resourceLinkId,
        string resourceId,
        string tag,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = Session.Query<LtiLineItem, LtiLineItemIndex>(index => index.CourseId == courseId && index.ToolId == toolId, collection: CollectionName);

        if (!string.IsNullOrEmpty(resourceLinkId))
        {
            query = query.Where(index => index.ResourceLinkId == resourceLinkId);
        }

        if (!string.IsNullOrEmpty(resourceId))
        {
            query = query.Where(index => index.ResourceId == resourceId);
        }

        if (!string.IsNullOrEmpty(tag))
        {
            query = query.Where(index => index.Tag == tag);
        }

        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(index => index.CreatedUtc).ThenBy(index => index.Id).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, items.ToArray());
    }

    /// <summary>
    /// Returns the line item of a resource link that belongs to a tool, the oldest when there are several.
    /// </summary>
    /// <param name="resourceLinkId">The resource link (course item).</param>
    /// <param name="toolId">The tool.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The line item, or <see langword="null"/>.</returns>
    public async Task<LtiLineItem> FindByResourceLinkAsync(string resourceLinkId, string toolId, CancellationToken cancellationToken = default)
        => await Session.Query<LtiLineItem, LtiLineItemIndex>(index => index.ResourceLinkId == resourceLinkId && index.ToolId == toolId, collection: CollectionName)
            .OrderBy(index => index.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);
}
