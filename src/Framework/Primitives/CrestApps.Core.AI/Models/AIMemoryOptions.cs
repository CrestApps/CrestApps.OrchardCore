namespace CrestApps.Core.AI.Models;

public sealed class AIMemoryOptions
{
    public string IndexProfileName { get; set; }

    public int TopN { get; set; } = 5;

    public AIMemoryOptions Clone()
        => new()
        {
            IndexProfileName = IndexProfileName,
            TopN = TopN,
        };
}
