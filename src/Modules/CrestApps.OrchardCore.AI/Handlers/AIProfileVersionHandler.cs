using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.AspNetCore.Http;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Handlers;

/// <summary>
/// Numbers each save of an AI profile and keeps the profile as it was saved, so its history can be compared and an
/// earlier version restored. The number is kept on the profile, and new conversations record it.
/// </summary>
internal sealed class AIProfileVersionHandler : CatalogEntryHandlerBase<AIProfile>
{
    private readonly IAIProfileVersionStore _versionStore;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    // The note a save carries, held from numbering to keeping so it never reaches the stored profile.
    private readonly Dictionary<string, string> _notes = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileVersionHandler"/> class.
    /// </summary>
    /// <param name="versionStore">The store the versions are kept in.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, which names who saved the profile.</param>
    /// <param name="clock">The clock.</param>
    public AIProfileVersionHandler(IAIProfileVersionStore versionStore, IHttpContextAccessor httpContextAccessor, IClock clock)
    {
        _versionStore = versionStore;
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
    }

    /// <inheritdoc/>
    public override Task CreatingAsync(CreatingContext<AIProfile> context, CancellationToken cancellationToken = default)
        => NumberAsync(context.Model, cancellationToken);

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<AIProfile> context, CancellationToken cancellationToken = default)
        => NumberAsync(context.Model, cancellationToken);

    /// <inheritdoc/>
    public override Task CreatedAsync(CreatedContext<AIProfile> context, CancellationToken cancellationToken = default)
        => KeepAsync(context.Model, cancellationToken);

    /// <inheritdoc/>
    public override Task UpdatedAsync(UpdatedContext<AIProfile> context, CancellationToken cancellationToken = default)
        => KeepAsync(context.Model, cancellationToken);

    private async Task NumberAsync(AIProfile profile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(profile?.ItemId))
        {
            return;
        }

        var metadata = profile.GetOrCreate<AIProfileVersionMetadata>();
        var latest = await _versionStore.GetLatestVersionAsync(profile.ItemId, cancellationToken);
        metadata.Version = Math.Max(latest, metadata.Version) + 1;
        _notes[profile.ItemId] = metadata.PendingNote;
        metadata.PendingNote = null;
        profile.Put(metadata);
    }

    private async Task KeepAsync(AIProfile profile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(profile?.ItemId) || !profile.TryGet<AIProfileVersionMetadata>(out var metadata) || metadata.Version <= 0)
        {
            return;
        }

        _notes.Remove(profile.ItemId, out var note);

        await _versionStore.SaveAsync(new AIProfileVersion
        {
            ItemId = UniqueId.GenerateId(),
            ProfileId = profile.ItemId,
            Version = metadata.Version,
            CreatedUtc = _clock.UtcNow,
            Author = _httpContextAccessor.HttpContext?.User?.Identity?.Name,
            Note = note,
            Snapshot = profile.Clone(),
        }, cancellationToken);
    }
}
