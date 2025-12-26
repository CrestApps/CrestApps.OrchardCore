using CrestApps.OrchardCore.AI.Chat.Models;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Chat.Indexes;

public sealed class CustomChatPartIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<CustomChatPartIndex>()
            .Map(contentItem =>
            {
                var part = contentItem.As<CustomChatPart>();
                if (part == null || !part.IsCustomInstance)
                {
                    return null;
                }

                return new CustomChatPartIndex
                {
                    ContentItemId = contentItem.ContentItemId,
                    CustomChatInstanceId = part.CustomChatInstanceId,
                    SessionId = part.SessionId,
                    UserId = part.UserId,
                    Source = part.Source,
                    ProviderName = part.ProviderName,
                    ConnectionName = part.ConnectionName,
                    DeploymentId = part.DeploymentId,
                    DisplayText = part.Title,
                    UseCaching = part.UseCaching,
                    IsCustomInstance = part.IsCustomInstance,
                    CreatedUtc = part.CreatedUtc
                };
            });
    }
}
