using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

public sealed class OmnichannelActivityContainer
{
    public OmnichannelActivity Activity { get; }

    public ContentItem Contact { get; }

    public ContentTypeDefinition SubjectContentTypeDefinition { get; }

    public User User { get; }

    public OmnichannelActivityContainer(OmnichannelActivity activity, ContentTypeDefinition subjectContentTypeDefinition, ContentItem contact, User user)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(subjectContentTypeDefinition);

        Activity = activity;
        SubjectContentTypeDefinition = subjectContentTypeDefinition;
        Contact = contact;
        User = user;
    }
}
