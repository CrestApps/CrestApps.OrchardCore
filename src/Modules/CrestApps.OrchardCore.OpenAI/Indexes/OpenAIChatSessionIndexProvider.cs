using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Core.Indexes;
using CrestApps.OrchardCore.OpenAI.Models;
using CrestApps.Support;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.OpenAI.Indexes;

public sealed class OpenAIChatSessionIndexProvider : IndexProvider<OpenAIChatSession>
{
    public OpenAIChatSessionIndexProvider()
    {
        CollectionName = OpenAIConstants.CollectionName;
    }

    public override void Describe(DescribeContext<OpenAIChatSession> context)
    {
        context
            .For<OpenAIChatSessionIndex>()
            .Map(session =>
            {
                return new OpenAIChatSessionIndex
                {
                    SessionId = session.SessionId,
                    ProfileId = session.ProfileId,
                    UserId = session.UserId,
                    ClientId = session.ClientId,
                    CreatedUtc = session.CreatedUtc,
                    Title = Str.Truncate(session.Title, 255),
                };
            });
    }
}
