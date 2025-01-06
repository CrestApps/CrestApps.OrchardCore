namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class ChatSessionQueryContext
{
    public string ProfileId { get; set; }

    public string Name { get; set; }

    public bool Sorted { get; set; }
}
