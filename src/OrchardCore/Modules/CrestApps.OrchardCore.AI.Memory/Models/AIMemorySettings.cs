namespace CrestApps.OrchardCore.AI.Memory.Models;

public sealed class AIMemorySettings
{
    public string IndexProfileName { get; set; }

    public int TopN { get; set; } = 5;
}
