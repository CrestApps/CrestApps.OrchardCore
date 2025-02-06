namespace CrestApps.Extensions.AI.DeepSeek.Models;

internal sealed class DeepSeekRequest
{
    public string Model { get; set; }

    public List<DeepSeekMessage> Messages { get; set; }

    public IList<DeepSeekChatTool> Tools { get; set; }

    public string ToolChoice { get; set; } = "auto";

    public float? Temperature { get; set; }

    public DeepSeekResponseFormat ResponseFormat { get; set; } = DeepSeekResponseFormat.Text;

    public bool Stream { get; set; }

    public float? TopP { get; set; }

    public float? FrequencyPenalty { get; set; }

    public float? PresencePenalty { get; set; }

    public int? MaxTokens { get; set; }
}
