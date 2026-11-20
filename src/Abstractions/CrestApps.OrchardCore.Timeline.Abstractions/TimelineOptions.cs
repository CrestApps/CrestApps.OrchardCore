using CrestApps.OrchardCore.Timeline.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Timeline;

/// <summary>
/// Settings of the timeline that modules set in code.
/// </summary>
public sealed class TimelineOptions
{
    /// <summary>
    /// Gets the content types whose timeline takes public comments. Public entries are read by everyone who may view the
    /// item, which on many sites includes every registered user, so a content type's timeline is internal only unless a
    /// module opts it in with <see cref="TimelineServiceCollectionExtensions.AllowPublicTimelineEntries"/>.
    /// </summary>
    public ISet<string> PublicContentTypes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the kinds of entry the timeline card's composer offers, in order. A note and a comment come first; a module
    /// adds its own with <see cref="TimelineServiceCollectionExtensions.AddTimelineComposerKind"/>. A module that needs
    /// more than a button (a reply that also sets a status, say) overrides the <c>TimelineComposer</c> shape for its
    /// record kind (<c>TimelineComposer__{Kind}</c>) and posts to its own action.
    /// </summary>
    public IList<TimelineComposerKind> ComposerKinds { get; } =
    [
        new()
        {
            Kind = TimelineConstants.Kinds.Note,
            Visibility = TimelineVisibility.Internal,
            ButtonText = LocalizationSource.Create<TimelineOptions>("Add note"),
        },
        new()
        {
            Kind = TimelineConstants.Kinds.Comment,
            Visibility = TimelineVisibility.Public,
            ButtonText = LocalizationSource.Create<TimelineOptions>("Add comment"),
        },
    ];
}
