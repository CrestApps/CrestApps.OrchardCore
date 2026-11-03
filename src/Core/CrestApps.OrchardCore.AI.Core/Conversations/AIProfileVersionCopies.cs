using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Runs an earlier version of an AI profile beside the current one, for a live split between versions: the version's
/// snapshot is copied once into a profile of its own, marked with <see cref="AIProfileVersionCopy"/>, which
/// conversations then use like any other profile.
/// </summary>
/// <remarks>
/// A copy rather than a version swapped in at run time, so every channel (website chat, text threads, both phone
/// engines) runs it unchanged. A version never changes once saved, so a copy is made once and reused. Sessions on a copy
/// record the source profile's version, so reports compare the versions of one profile.
/// </remarks>
public sealed class AIProfileVersionCopies
{
    private const int VersionPageSize = 100;

    private readonly IAIProfileManager _profileManager;
    private readonly IAIProfileVersionStore _versionStore;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileVersionCopies"/> class.
    /// </summary>
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="versionStore">The profile version store.</param>
    /// <param name="logger">The logger.</param>
    public AIProfileVersionCopies(
        IAIProfileManager profileManager,
        IAIProfileVersionStore versionStore,
        ILogger<AIProfileVersionCopies> logger)
    {
        _profileManager = profileManager;
        _versionStore = versionStore;
        _logger = logger;
    }

    /// <summary>
    /// The technical name of a version's copy.
    /// </summary>
    /// <param name="profileName">The source profile's technical name.</param>
    /// <param name="version">The version.</param>
    /// <returns>The name.</returns>
    public static string CopyName(string profileName, int version)
        => $"{profileName}-v{version}";

    /// <summary>
    /// Gets the profile that runs a version of a profile: the profile itself when the version is its current one, and
    /// otherwise the version's copy, made on first use.
    /// </summary>
    /// <param name="profileId">The source profile.</param>
    /// <param name="version">The version to run.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The profile to use, or <see langword="null"/> when the profile or the version does not exist.</returns>
    public async Task<AIProfile> GetOrCreateAsync(string profileId, int version, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profileId) || version <= 0)
        {
            return null;
        }

        var profile = await _profileManager.FindByIdAsync(profileId, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        if (profile.TryGet<AIProfileVersionMetadata>(out var current) && current.Version == version)
        {
            return profile;
        }

        var name = CopyName(profile.Name, version);
        var existing = await _profileManager.FindByNameAsync(name, cancellationToken);

        if (existing is not null && existing.TryGet<AIProfileVersionCopy>(out var marker) && marker.SourceProfileId == profileId && marker.Version == version)
        {
            return existing;
        }

        var snapshot = await FindVersionAsync(profileId, version, cancellationToken);

        if (snapshot?.Snapshot is null)
        {
            _logger.LogWarning(
                "Version {Version} of AI profile '{ProfileId}' was not found, so it cannot be run.",
                version,
                profileId.SanitizeLogValue());

            return null;
        }

        var copy = snapshot.Snapshot.Clone();
        copy.ItemId = null;
        copy.Name = name;
        copy.DisplayText = $"{(string.IsNullOrEmpty(profile.DisplayText) ? profile.Name : profile.DisplayText)} (version {version})";
        copy.CreatedUtc = default;
        copy.ModifiedUtc = null;
        copy.Properties.Remove(nameof(AIProfileVersionMetadata));
        copy.Put(new AIProfileVersionCopy
        {
            SourceProfileId = profileId,
            Version = version,
        });

        var created = await _profileManager.NewAsync(name, cancellationToken: cancellationToken);
        AIProfileVersionCopyMapper.CopyInto(copy, created);
        await _profileManager.CreateAsync(created, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Version {Version} of AI profile '{ProfileId}' was copied to run beside the current version.",
                version,
                profileId.SanitizeLogValue());
        }

        return created;
    }

    private async Task<AIProfileVersion> FindVersionAsync(string profileId, int version, CancellationToken cancellationToken)
    {
        for (var page = 1; ; page++)
        {
            var (versions, count) = await _versionStore.PageAsync(profileId, page, VersionPageSize, cancellationToken);
            var match = versions.FirstOrDefault(candidate => candidate.Version == version);

            if (match is not null)
            {
                return match;
            }

            if (versions.Count == 0 || page * VersionPageSize >= count)
            {
                return null;
            }
        }
    }
}

/// <summary>
/// Copies a profile's configuration into a new profile record, keeping the new record's identity.
/// </summary>
internal static class AIProfileVersionCopyMapper
{
    /// <summary>
    /// Copies everything a conversation reads from <paramref name="source"/> into <paramref name="target"/>.
    /// </summary>
    /// <param name="source">The configuration to copy.</param>
    /// <param name="target">The new record.</param>
    public static void CopyInto(AIProfile source, AIProfile target)
    {
        target.DisplayText = source.DisplayText;
        target.Description = source.Description;
        target.Type = source.Type;
        target.OrchestratorName = source.OrchestratorName;
        target.ChatDeploymentName = source.ChatDeploymentName;
        target.UtilityDeploymentName = source.UtilityDeploymentName;
        target.TitleType = source.TitleType;
        target.WelcomeMessage = source.WelcomeMessage;
        target.PromptSubject = source.PromptSubject;
        target.PromptTemplate = source.PromptTemplate;
        target.Properties = source.Properties;
        target.Settings = source.Settings;
    }
}
