using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class ReceivedUpdateContext
{
    public ReceivedUpdateContext(StreamingChatCompletionUpdate update, AIProfile profile, string prompt)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(profile);

        Update = update;
        Profile = profile;
        Prompt = prompt;
    }

    public StreamingChatCompletionUpdate Update { get; }

    public AIProfile Profile { get; }

    public string Prompt { get; }
}
