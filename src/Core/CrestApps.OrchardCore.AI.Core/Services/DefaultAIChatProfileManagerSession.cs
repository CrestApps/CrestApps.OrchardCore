using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAIChatProfileManagerSession : IAIChatProfileManagerSession

{
    private readonly Dictionary<string, AIChatProfile> _profiles = [];

    public void Clear()
    {
        _profiles.Clear();
    }

    public bool Forget(string id)
        => _profiles.Remove(id);

    public bool Recall(string id, out AIChatProfile profile)
        => _profiles.TryGetValue(id, out profile);

    public void Store(AIChatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        _profiles[profile.Id] = profile;
    }
}
