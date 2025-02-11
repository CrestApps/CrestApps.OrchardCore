namespace CrestApps.OrchardCore.AI.Models;

public class AIProfileResult
{
    public int Count { get; set; }

    public IEnumerable<AIProfile> Profiles { get; set; }
}
