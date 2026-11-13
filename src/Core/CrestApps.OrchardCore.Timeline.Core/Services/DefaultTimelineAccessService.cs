using System.Security.Claims;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;

namespace CrestApps.OrchardCore.Timeline.Core.Services;

/// <summary>
/// The default <see cref="ITimelineAccessService"/>: asks the provider of the record's kind, and refuses a kind that has
/// none.
/// </summary>
public sealed class DefaultTimelineAccessService : ITimelineAccessService
{
    private readonly IEnumerable<ITimelineAccessProvider> _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTimelineAccessService"/> class.
    /// </summary>
    /// <param name="providers">The access providers.</param>
    public DefaultTimelineAccessService(IEnumerable<ITimelineAccessProvider> providers)
    {
        _providers = providers;
    }

    /// <inheritdoc/>
    public Task<bool> CanViewAsync(ClaimsPrincipal user, RecordRef subject, TimelineVisibility visibility, CancellationToken cancellationToken = default)
    {
        var provider = Find(subject);

        return user is null || provider is null
            ? Task.FromResult(false)
            : provider.CanViewAsync(user, subject, visibility, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> CanAddAsync(ClaimsPrincipal user, RecordRef subject, string kind, TimelineVisibility visibility, CancellationToken cancellationToken = default)
    {
        var provider = Find(subject);

        return user is null || provider is null || string.IsNullOrEmpty(kind)
            ? Task.FromResult(false)
            : provider.CanAddAsync(user, subject, kind, visibility, cancellationToken);
    }

    private ITimelineAccessProvider Find(RecordRef subject)
        => subject.IsEmpty
            ? null
            : _providers.FirstOrDefault(provider => string.Equals(provider.SubjectKind, subject.Kind, StringComparison.Ordinal));
}
