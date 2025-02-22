namespace CrestApps.OrchardCore.AI.Models;

public class AIToolInstancesResult
{
    public int Count { get; set; }

    public IEnumerable<AIToolInstance> Instances { get; set; }
}
