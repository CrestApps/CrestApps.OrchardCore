namespace CrestApps.OrchardCore.AI.Models;

public class AIChatProfileResult
{
    public int Count { get; set; }

    public IEnumerable<AIChatProfile> Profiles { get; set; }
}
