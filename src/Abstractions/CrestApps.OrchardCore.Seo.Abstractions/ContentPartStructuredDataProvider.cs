using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Seo;

/// <summary>
/// A base class for a provider that describes one content part. It only runs for content items that have the part.
/// </summary>
/// <typeparam name="TPart">The content part type.</typeparam>
public abstract class ContentPartStructuredDataProvider<TPart> : IStructuredDataProvider
    where TPart : ContentPart, new()
{
    /// <inheritdoc/>
    public virtual int Order => StructuredDataProviderOrder.Parts;

    /// <inheritdoc/>
    public Task BuildAsync(StructuredDataContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.MainEntity is null ||
            context.ContentItem is null ||
            !context.ContentItem.TryGet<TPart>(out var part))
        {
            return Task.CompletedTask;
        }

        return BuildAsync(context, part);
    }

    /// <summary>
    /// Adds the part's data to the context.
    /// </summary>
    /// <param name="context">The structured data context. Its <see cref="StructuredDataContext.MainEntity"/> is set.</param>
    /// <param name="part">The part of the content item.</param>
    /// <returns>A task that completes when the data is added.</returns>
    protected abstract Task BuildAsync(StructuredDataContext context, TPart part);
}
