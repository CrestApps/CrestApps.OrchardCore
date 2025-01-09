namespace CrestApps.OrchardCore.OpenAI.Models;

public class OpenAIChatSessionResult
{
    public int Count { get; set; }

    public IEnumerable<OpenAIChatSession> Sessions { get; set; }
}
