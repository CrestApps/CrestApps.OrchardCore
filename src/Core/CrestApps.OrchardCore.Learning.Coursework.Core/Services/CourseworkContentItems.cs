using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Coursework.Core.Services;

/// <summary>
/// Reads a part of a content item that may be missing, or of an item that may not exist.
/// </summary>
public static class CourseworkContentItems
{
    /// <summary>
    /// Gets a part of a content item.
    /// </summary>
    /// <typeparam name="TPart">The part type.</typeparam>
    /// <param name="item">The content item; may be <see langword="null"/>.</param>
    /// <returns>The part, or <see langword="null"/> when the item is missing or has no such part.</returns>
    public static TPart FindPart<TPart>(this ContentItem item)
        where TPart : ContentPart
        => item is not null && item.TryGet<TPart>(out var part) ? part : null;
}
