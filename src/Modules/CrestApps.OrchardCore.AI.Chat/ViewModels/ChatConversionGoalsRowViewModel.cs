namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

public class ChatConversionGoalsRowViewModel
{
    public string SessionId { get; set; }

    public DateTime SessionStartedUtc { get; set; }

    public string TotalPoints { get; set; }

    public Dictionary<string, string> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
