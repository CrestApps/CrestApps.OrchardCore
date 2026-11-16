using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.Learning.AI.Models;

namespace CrestApps.OrchardCore.Learning.AI.Services;

/// <summary>
/// Tells whether the AI profile of a request is a learning tutor.
/// </summary>
internal sealed class LearningTutorProfiles
{
    private readonly IAIProfileStore _profileStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningTutorProfiles"/> class.
    /// </summary>
    /// <param name="profileStore">The AI profile store.</param>
    public LearningTutorProfiles(IAIProfileStore profileStore)
    {
        _profileStore = profileStore;
    }

    /// <summary>
    /// Gets whether a profile is marked as a learning tutor.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <returns><see langword="true"/> for a tutor.</returns>
    public static bool IsTutor(AIProfile profile)
        => profile is not null && profile.TryGet<LearningTutorProfileMetadata>(out var metadata) && metadata.IsTutor;

    /// <summary>
    /// Gets whether the resource of a completion, or the profile of its chat session, is a learning tutor.
    /// </summary>
    /// <param name="resource">The resource of the completion.</param>
    /// <param name="profileId">The profile of the chat session.</param>
    /// <returns><see langword="true"/> for a tutor.</returns>
    public async Task<bool> IsTutorAsync(object resource, string profileId)
    {
        if (resource is AIProfile profile)
        {
            return IsTutor(profile);
        }

        if (resource is not null || string.IsNullOrEmpty(profileId))
        {
            return false;
        }

        return IsTutor(await _profileStore.FindByIdAsync(profileId));
    }
}
