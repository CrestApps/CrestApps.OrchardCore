namespace CrestApps.AI.Models;

public sealed class ChatInteractionMemoryOptions
{
    public bool EnableUserMemory { get; set; } = true;

    public ChatInteractionMemoryOptions Clone()
        => new()
        {
            EnableUserMemory = EnableUserMemory,
        };

    public static ChatInteractionMemoryOptions FromSettings(ChatInteractionMemorySettings settings)
        => settings == null
            ? new ChatInteractionMemoryOptions()
            : new ChatInteractionMemoryOptions
            {
                EnableUserMemory = settings.EnableUserMemory,
            };
}
