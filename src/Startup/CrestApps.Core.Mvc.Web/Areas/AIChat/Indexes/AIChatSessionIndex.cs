using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql.Indexes;
using YesSql.Indexes;

namespace CrestApps.Core.Mvc.Web.Areas.AIChat.Indexes;

public sealed class AIChatSessionIndex : CatalogItemIndex
{
    public string SessionId { get; set; }

    public string ProfileId { get; set; }

    public string UserId { get; set; }

    public int Status { get; set; }

    public DateTime LastActivityUtc { get; set; }
}

public sealed class AIChatSessionIndexProvider : IndexProvider<AIChatSession>
{
    public override void Describe(DescribeContext<AIChatSession> context)
    {
        context.For<AIChatSessionIndex>()
            .Map(session => new AIChatSessionIndex
            {
                ItemId = session.SessionId,
                SessionId = session.SessionId,
                ProfileId = session.ProfileId,
                UserId = session.UserId,
                Status = (int)session.Status,
                LastActivityUtc = session.LastActivityUtc,
            });
    }
}
