using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.AI.Core.Models;

public sealed class AIProfilePart : ContentPart
{
    public string ProfileId { get; set; }

    public int? TotalHistory { get; set; }
}
