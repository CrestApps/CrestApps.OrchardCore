namespace CrestApps.OrchardCore.AI.Models;

public class AIChatSessionResult
{
    public int Count { get; set; }

    public IEnumerable<AIChatSessionEntry> Sessions { get; set; }
}
