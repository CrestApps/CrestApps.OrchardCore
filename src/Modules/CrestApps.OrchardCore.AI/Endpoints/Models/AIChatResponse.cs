namespace CrestApps.OrchardCore.AI.Endpoints.Models;

internal sealed class AIChatResponse
{
    public bool Success { get; set; }

    public string Type { get; set; }

    public string SessionId { get; set; }

    public bool IsNew { get; set; }

    public AIChatResponseMessageDetailed Message { get; set; }
}
