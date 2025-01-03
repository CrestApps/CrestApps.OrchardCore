using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI.Core.Services;

public sealed class DefaultOpenAIChatProfileManagerSession : IOpenAIChatProfileManagerSession
{
    private readonly Dictionary<string, OpenAIChatProfile> _profiles = [];

    public void Clear()
    {
        _profiles.Clear();
    }

    public bool Forget(string id)
        => _profiles.Remove(id);

    public bool Recall(string id, out OpenAIChatProfile profile)
        => _profiles.TryGetValue(id, out profile);

    public void Store(OpenAIChatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        _profiles[profile.Id] = profile;
    }
}
