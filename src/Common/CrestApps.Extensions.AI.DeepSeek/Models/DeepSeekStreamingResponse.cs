namespace CrestApps.Extensions.AI.DeepSeek.Models;

internal sealed class DeepSeekStreamingResponse
{
    public string Id { get; set; }

    public string Model { get; set; }

    public long Created { get; set; }

    public List<DeepSeekStreamingChoice> Choices { get; set; }
}
