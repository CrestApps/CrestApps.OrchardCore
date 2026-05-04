using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Contains the context for an omnichannel activity, including the activity, contact, content type definition, and associated user.
/// </summary>
public sealed class OmnichannelActivityContainer
{
    /// <summary>
    /// Gets the omnichannel activity.
    /// </summary>
    public OmnichannelActivity Activity { get; }

    /// <summary>
    /// Gets the contact content item associated with the activity.
    /// </summary>
    public ContentItem Contact { get; }

    /// <summary>
    /// Gets the content type definition for the activity subject.
    /// </summary>
    public ContentTypeDefinition SubjectContentTypeDefinition { get; }

    /// <summary>
    /// Gets the user associated with the activity.
    /// </summary>
    public User User { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelActivityContainer"/> class.
    /// </summary>
    /// <param name="activity">The omnichannel activity.</param>
    /// <param name="subjectContentTypeDefinition">The content type definition for the activity subject.</param>
    /// <param name="contact">The contact content item.</param>
    /// <param name="user">The user associated with the activity.</param>
    public OmnichannelActivityContainer(
        OmnichannelActivity activity,
        ContentTypeDefinition subjectContentTypeDefinition,
        ContentItem contact,
        User user)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(subjectContentTypeDefinition);

        Activity = activity;
        SubjectContentTypeDefinition = subjectContentTypeDefinition;
        Contact = contact;
        User = user;
    }
}
