using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Core.Indexes;
using CrestApps.OrchardCore.OpenAI.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.OpenAI.Indexes;

public sealed class AIChatSessionIndexProvider : IndexProvider<AIChatSession>
{
    public AIChatSessionIndexProvider()
    {
        CollectionName = OpenAIConstants.CollectionName;
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
                    CreatedAtUtc = session.CreatedUtc,
                    Title = session.Title,
                };
            });
    }
}
