namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Represents an immutable snapshot of the content types that have the omnichannel subject or contact part
/// attached. The snapshot is stored in the per-tenant memory cache and replaced through copy-on-write so that
/// readers that captured a reference keep observing a consistent set while it is being updated.
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

    /// <summary>
    /// Produces a new snapshot with the membership of the specified content type applied, or returns the current
    /// snapshot unchanged when the membership already matches.
    /// </summary>
    /// <param name="subject"><see langword="true"/> to update the subject set; <see langword="false"/> to update the contact set.</param>
    /// <param name="contentType">The technical name of the content type whose membership is changing.</param>
    /// <param name="isMember"><see langword="true"/> when the content type should be a member; otherwise, <see langword="false"/>.</param>
    /// <returns>A new <see cref="OmnichannelContentTypeSet"/> reflecting the change, or the same instance when nothing changed.</returns>
    public OmnichannelContentTypeSet With(bool subject, string contentType, bool isMember)
    {
        var target = subject ? SubjectContentTypes : ContactContentTypes;

        if (isMember == target.Contains(contentType))
        {
            return this;
        }

        var updated = new HashSet<string>(target, StringComparer.Ordinal);

        if (isMember)
        {
            updated.Add(contentType);
        }
        else
        {
            updated.Remove(contentType);
        }

        return subject
            ? new OmnichannelContentTypeSet(updated, ContactContentTypes)
            : new OmnichannelContentTypeSet(SubjectContentTypes, updated);
    }
}
