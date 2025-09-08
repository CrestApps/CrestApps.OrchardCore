using OrchardCore.ContentManagement;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

public sealed class OmnichannelActivityContainer
{
    public OmnichannelActivity Activity { get; }

    public ContentItem Subject { get; }

    public ContentItem Contact { get; }

    public User User { get; }

    public OmnichannelActivityContainer(OmnichannelActivity activity, ContentItem contact, User user, ContentItem subject)
    {
        ArgumentNullException.ThrowIfNull(activity);

        Activity = activity;
        Subject = subject;
        Contact = contact;
        User = user;
    }
}
