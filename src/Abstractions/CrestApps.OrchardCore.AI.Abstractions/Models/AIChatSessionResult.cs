namespace CrestApps.OrchardCore.AI.Models;

public class AIChatSessionResult
{
    public int Count { get; set; }

    public IEnumerable<AIChatSession> Sessions { get; set; }
}
