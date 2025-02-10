using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIProfileManagerSession : IAIProfileManagerSession

{
    private readonly Dictionary<string, AIProfile> _profiles = [];

    public void Clear()
    {
        _profiles.Clear();
    }

    public bool Forget(string id)
        => _profiles.Remove(id);

    public bool Recall(string id, out AIProfile profile)
        => _profiles.TryGetValue(id, out profile);

    public void Store(AIProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        _profiles[profile.Id] = profile;
    }
}
