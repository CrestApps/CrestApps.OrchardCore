namespace CrestApps.Core.AI.Models;

public sealed class ChatInteractionMemoryOptions
{
    public bool EnableUserMemory { get; set; } = true;

    public ChatInteractionMemoryOptions Clone()
        => new()
        {
            EnableUserMemory = EnableUserMemory,
        };

    public static ChatInteractionMemoryOptions FromMetadata(MemoryMetadata metadata)
        => new()
        {
            EnableUserMemory = metadata?.EnableUserMemory ?? true,
        };
}
