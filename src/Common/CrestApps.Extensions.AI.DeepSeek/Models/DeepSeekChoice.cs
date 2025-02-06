namespace CrestApps.Extensions.AI.DeepSeek.Models;

internal sealed class DeepSeekChoice
{
    public DeepSeekMessage Message { get; set; }

    public string FinishReason { get; set; }

    public int Index { get; set; }
}
