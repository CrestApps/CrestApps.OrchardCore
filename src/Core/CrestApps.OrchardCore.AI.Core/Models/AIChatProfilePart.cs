using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.AI.Core.Models;

public sealed class AIChatProfilePart : ContentPart
{
    public string ProfileId { get; set; }

    public int? TotalHistory { get; set; }
}
