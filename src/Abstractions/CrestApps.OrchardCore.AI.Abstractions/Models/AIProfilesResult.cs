namespace CrestApps.OrchardCore.AI.Models;

public class AIProfilesResult
{
    public int Count { get; set; }

    public IEnumerable<AIProfile> Profiles { get; set; }
}
