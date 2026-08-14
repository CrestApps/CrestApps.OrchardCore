using CrestApps.Core.Infrastructure.Indexing;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Core;

internal sealed class IndexProfileInfoAdapter : IIndexProfileInfo
{
    private readonly IndexProfile _profile;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexProfileInfoAdapter"/> class.
    /// </summary>
    /// <param name="profile">The index profile to adapt.</param>
    public IndexProfileInfoAdapter(IndexProfile profile)
    {
        _profile = profile;
    }

    public string IndexProfileId => _profile.Id;
    public string IndexName => _profile.IndexName;
    public string ProviderName => _profile.ProviderName;
    public string IndexFullName => _profile.IndexFullName;
}

/// <summary>
/// Extension methods for converting <see cref="IndexProfile"/> instances to <see cref="IIndexProfileInfo"/>.
/// </summary>
public static class IndexProfileExtensions
{
    /// <summary>
    /// Wraps the given <see cref="IndexProfile"/> in an <see cref="IIndexProfileInfo"/> adapter.
    /// </summary>
    /// <param name="profile">The index profile to convert.</param>
    public static IIndexProfileInfo ToIndexProfileInfo(this IndexProfile profile)
        => new IndexProfileInfoAdapter(profile);
}
