namespace CrestApps.Extensions.AI.DeepSeek.Models;

internal sealed class DeepSeekStreamingChoice
{
    public int Index { get; set; }

    public DeepSeekStreamingDelta Delta { get; set; }

    public string FinishReason { get; set; }
}
