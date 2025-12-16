using CrestApps.OrchardCore.YesSql.Core.Indexes;

namespace CrestApps.OrchardCore.AI.Chat.Indexes;

public sealed class CustomChatPartIndex : CatalogItemIndex, ISourceAwareIndex, IDisplayTextAwareIndex
{
    public string CustomChatInstanceId { get; set; }


    public string SessionId { get; set; }


    public string UserId { get; set; }


    public string Source { get; set; }


    public string ProviderName { get; set; }


    public string ConnectionName { get; set; }


    public string DeploymentId { get; set; }


    public string DisplayText { get; set; }


    public bool IsCustomInstance { get; set; }


    public DateTime CreatedUtc { get; set; }
}
