namespace CrestApps.OrchardCore.DeepSeek.Core.Models;

internal sealed class DeepSeekResponse
{
    public string Id { get; set; }

    public string Object { get; set; }

    public long Created { get; set; }

    public string Model { get; set; }

    public DeepSeekUsage Usage { get; set; }

    public List<DeepSeekChoice> Choices { get; set; }
}
