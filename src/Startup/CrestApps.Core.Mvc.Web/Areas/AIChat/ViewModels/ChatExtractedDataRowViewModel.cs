namespace CrestApps.Core.Mvc.Web.Areas.AIChat.ViewModels;

public sealed class ChatExtractedDataRowViewModel
{
    public DateTime SessionStartedUtc { get; set; }

    public string SessionId { get; set; }

    public IReadOnlyDictionary<string, string> Values { get; set; } = new Dictionary<string, string>();
}
