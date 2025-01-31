using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.AI.Core.Models;

public sealed class AIChatWidgetPart : ContentPart
{
    public string ProfileId { get; set; }

    public int? TotalHistory { get; set; }
}
