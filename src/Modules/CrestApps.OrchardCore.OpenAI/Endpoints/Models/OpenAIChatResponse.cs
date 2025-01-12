namespace CrestApps.OrchardCore.OpenAI.Endpoints.Models;

internal sealed class OpenAIChatResponse
{
    public bool Success { get; set; }

    public string Type { get; set; }

    public string SessionId { get; set; }

    public bool IsNew { get; set; }

    public OpenAIChatResponseMessageDetailed Message { get; set; }
}
