using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI;

public sealed class AIChatSessionDocumentAuthorizationContext
{
    public AIChatSessionDocumentAuthorizationContext(AIProfile profile, AIChatSession session)
    {
        Profile = profile;
        Session = session;
    }

    public AIProfile Profile { get; }

    public AIChatSession Session { get; }
}
