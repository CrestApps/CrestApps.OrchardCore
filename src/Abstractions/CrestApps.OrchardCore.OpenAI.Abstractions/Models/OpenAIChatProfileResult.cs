namespace CrestApps.OrchardCore.OpenAI.Models;

public class OpenAIChatProfileResult
{
    public int Count { get; set; }

    public IEnumerable<OpenAIChatProfile> Profiles { get; set; }
}
