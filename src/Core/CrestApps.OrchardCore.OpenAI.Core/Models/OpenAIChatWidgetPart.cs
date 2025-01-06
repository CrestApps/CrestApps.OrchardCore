using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.OpenAI.Core.Models;

public sealed class OpenAIChatWidgetPart : ContentPart
{
    public string ProfileId { get; set; }

    public int? TotalHistory { get; set; }
}
