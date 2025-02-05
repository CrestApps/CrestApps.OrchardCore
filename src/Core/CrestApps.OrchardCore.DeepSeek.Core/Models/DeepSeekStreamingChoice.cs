namespace CrestApps.OrchardCore.DeepSeek.Core.Models;

internal sealed class DeepSeekStreamingChoice
{
    public int Index { get; set; }

    public DeepSeekStreamingDelta Delta { get; set; }

    public string FinishReason { get; set; }
}
