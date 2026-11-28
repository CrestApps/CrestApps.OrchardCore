using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

/// <summary>
/// The fields a content tool returns for one content item version in a list. It never carries the item's body.
/// </summary>
internal sealed class ContentItemSummary
{
    /// <summary>
    /// Gets or sets the content item ID.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the ID of this version of the content item.
    /// </summary>
    public string ContentItemVersionId { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the content type.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the title of the content item.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this version is the published one.
    /// </summary>
    public bool Published { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this version is the latest one.
    /// </summary>
    public bool Latest { get; set; }

    /// <summary>
    /// Gets or sets the user ID of the owner.
    /// </summary>
    public string Owner { get; set; }

    /// <summary>
    /// Gets or sets the user name of the person who last saved this version.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the local time the content item was created.
    /// </summary>
    public string Created { get; set; }

    /// <summary>
    /// Gets or sets the local time this version was last modified.
    /// </summary>
    public string Modified { get; set; }

    /// <summary>
    /// Gets or sets the local time this version was published.
    /// </summary>
    public string PublishedAt { get; set; }

    /// <summary>
    /// Builds the summary of a content item version, with its dates in the user's or the site's time zone.
    /// </summary>
    /// <param name="contentItem">The content item version.</param>
    /// <param name="localClock">The local clock.</param>
    /// <returns>The summary.</returns>
    public static async Task<ContentItemSummary> CreateAsync(ContentItem contentItem, ILocalClock localClock)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        return new ContentItemSummary
        {
            ContentItemId = contentItem.ContentItemId,
            ContentItemVersionId = contentItem.ContentItemVersionId,
            ContentType = contentItem.ContentType,
            DisplayText = contentItem.DisplayText,
            Published = contentItem.Published,
            Latest = contentItem.Latest,
            Owner = contentItem.Owner,
            Author = contentItem.Author,
            Created = await AgentToolResults.ToLocalAsync(localClock, contentItem.CreatedUtc),
            Modified = await AgentToolResults.ToLocalAsync(localClock, contentItem.ModifiedUtc),
            PublishedAt = await AgentToolResults.ToLocalAsync(localClock, contentItem.PublishedUtc),
        };
    }
}
