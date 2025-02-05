namespace CrestApps.OrchardCore.DeepSeek.Core.Models;

internal sealed class DeepSeekResponseFormat
{
    public string Type { get; set; } = "text";

    public static DeepSeekResponseFormat Text => new DeepSeekResponseFormat { Type = "text" };

    public static DeepSeekResponseFormat Json => new DeepSeekResponseFormat { Type = "json_object" };
}
