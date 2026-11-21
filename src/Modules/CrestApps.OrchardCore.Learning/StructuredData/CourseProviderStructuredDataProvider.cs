using CrestApps.OrchardCore.Seo;

namespace CrestApps.OrchardCore.Learning.StructuredData;

/// <summary>
/// Names the site's publisher as the <c>provider</c> of a <c>Course</c>. It runs after the site provider, which adds the
/// publisher node to the page's graph, and refers to that node; a page whose site sets no publisher gets no provider.
/// </summary>
internal sealed class CourseProviderStructuredDataProvider : IStructuredDataProvider
{
    /// <inheritdoc/>
    public int Order => StructuredDataProviderOrder.Site + 10;

    /// <inheritdoc/>
    public Task BuildAsync(StructuredDataContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var entity = context.MainEntity;

        if (entity is null || !string.Equals(JsonLd.GetType(entity), "Course", StringComparison.Ordinal) || entity["provider"] is not null)
        {
            return Task.CompletedTask;
        }

        var publisherId = StructuredDataNodeIds.Publisher(context.BaseUrl);

        if (context.Graph.Find(publisherId) is not null)
        {
            entity["provider"] = JsonLd.Reference(publisherId);
        }

        return Task.CompletedTask;
    }
}
