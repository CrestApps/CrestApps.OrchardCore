using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.Support;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Indexes;

internal sealed class AIChatSessionIndexProvider : IndexProvider<AIChatSession>
{
    public AIChatSessionIndexProvider()
    {
        CollectionName = AIConstants.CollectionName;
    }

    public override void Describe(DescribeContext<AIChatSession> context)
    {
        context
            .For<AIChatSessionIndex>()
            .Map(session =>
            {
                return new AIChatSessionIndex
                {
                    SessionId = session.SessionId,
                    ProfileId = session.ProfileId,
                    UserId = session.UserId,
                    ClientId = session.ClientId,
                    CreatedUtc = session.CreatedUtc,
                    Title = Str.Truncate(session.Title, 255),
                    Status = session.Status,
                    LastActivityUtc = session.LastActivityUtc,
                };
            });
    }
}
