using CrestApps.AI;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Core;

internal sealed class IndexProfileInfoAdapter : IIndexProfileInfo
{
    private readonly IndexProfile _profile;

    public IndexProfileInfoAdapter(IndexProfile profile)
    {
        _profile = profile;
    }

    public string IndexProfileId => _profile.Id;
    public string IndexName => _profile.IndexName;
    public string ProviderName => _profile.ProviderName;
    public string IndexFullName => _profile.IndexFullName;
}

public static class IndexProfileExtensions
{
    public static IIndexProfileInfo ToIndexProfileInfo(this IndexProfile profile)
        => new IndexProfileInfoAdapter(profile);
}