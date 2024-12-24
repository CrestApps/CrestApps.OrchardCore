namespace CrestApps.OrchardCore.OpenAI.Models;

public class AIProfileResult
{
    public int Count { get; set; }

    public IEnumerable<AIChatProfile> Profiles { get; set; }
}
