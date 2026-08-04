namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Represents an immutable snapshot of the content types that have the omnichannel subject or contact part
/// attached. The snapshot is stored in the per-tenant memory cache and replaced wholesale when the cache entry is
/// invalidated after a content definition change.
/// </summary>
internal sealed class OmnichannelContentTypeSet
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContentTypeSet"/> class.
    /// </summary>
    /// <param name="subjectContentTypes">The technical names of the content types that attach the subject part.</param>
    /// <param name="contactContentTypes">The technical names of the content types that attach the contact part.</param>
    public OmnichannelContentTypeSet(
        HashSet<string> subjectContentTypes,
        HashSet<string> contactContentTypes)
    {
        SubjectContentTypes = subjectContentTypes;
        ContactContentTypes = contactContentTypes;
    }

    /// <summary>
    /// Gets the technical names of the content types that attach the omnichannel subject part.
    /// </summary>
    public HashSet<string> SubjectContentTypes { get; }

    /// <summary>
    /// Gets the technical names of the content types that attach the omnichannel contact part.
    /// </summary>
    public HashSet<string> ContactContentTypes { get; }
}
