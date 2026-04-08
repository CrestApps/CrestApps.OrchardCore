using CrestApps.Core.AI.Models;
using CrestApps.Core.Support;
using YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql.Indexes.AIChat;

public sealed class AIChatSessionIndex : MapIndex
{
    public string SessionId { get; set; }

    public string ProfileId { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Title { get; set; }

    public string UserId { get; set; }

    public string ClientId { get; set; }

    public ChatSessionStatus Status { get; set; }

    public PostSessionProcessingStatus PostSessionProcessingStatus { get; set; }

    public DateTime LastActivityUtc { get; set; }
}

public sealed class AIChatSessionIndexProvider : IndexProvider<AIChatSession>
{
    public AIChatSessionIndexProvider()
    {
        CollectionName = OrchardCoreAICollectionNames.AI;
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
                    PostSessionProcessingStatus = session.PostSessionProcessingStatus,
                    LastActivityUtc = session.LastActivityUtc,
                };
            });
    }
}
