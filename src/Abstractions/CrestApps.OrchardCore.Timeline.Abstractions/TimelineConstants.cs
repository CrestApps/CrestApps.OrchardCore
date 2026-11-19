namespace CrestApps.OrchardCore.Timeline;

/// <summary>
/// Names shared by the Timeline building block and the modules that build on it.
/// </summary>
public static class TimelineConstants
{
    /// <summary>
    /// The notification type of a mention (<c>@username</c>) in a timeline entry, on any record. It is stored on the
    /// notifications, so it never changes.
    /// </summary>
    public const string MentionNotificationType = "TimelineMention";

    /// <summary>
    /// The entry properties the timeline card reads.
    /// </summary>
    public static class Properties
    {
        /// <summary>
        /// A short warning shown beside the entry, such as that an e-mail with it couldn't be delivered. A module sets
        /// it with <c>ITimelineService.UpdateAsync</c> and removes it when it no longer applies.
        /// </summary>
        public const string Notice = "Notice";
    }

    /// <summary>
    /// The YesSql collection that holds timeline entries.
    /// </summary>
    public const string CollectionName = "Timeline";

    /// <summary>
    /// The name of the recipe step, and of the deployment step, that carries timeline entries.
    /// </summary>
    public const string RecipeStepName = "TimelineEntries";

    /// <summary>
    /// The longest body an entry may have, in characters.
    /// </summary>
    public const int MaxBodyLength = 20_000;

    /// <summary>
    /// The most characters of an entry's kind, channel, and record and author kinds and ids, which are indexed.
    /// </summary>
    public const int MaxIndexedLength = 64;

    /// <summary>
    /// The most characters of <see cref="Models.TimelineEntry.ExternalId"/>, which is indexed.
    /// </summary>
    public const int MaxExternalIdLength = 255;

    /// <summary>
    /// The longest text a refused post keeps for the next page. The draft travels in the TempData cookie, and a long
    /// one would push the request headers past what servers and proxies accept and lock the user out of the site.
    /// </summary>
    public const int MaxKeptDraftLength = 2_000;

    /// <summary>
    /// The record kind of a content item's timeline, such as an account, a contact or a lead; the record id is the
    /// content item id.
    /// </summary>
    public const string ContentItemKind = Records.RecordKinds.ContentItem;

    /// <summary>
    /// The owner kind of the files attached to a timeline entry; the owner id is the entry's id, so the files are as
    /// visible as the entry.
    /// </summary>
    public const string AttachmentOwnerKind = "TimelineEntry";

    /// <summary>
    /// The feature identifiers of the Timeline module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The Timeline feature.
        /// </summary>
        public const string Timeline = "CrestApps.OrchardCore.Timeline";
    }

    /// <summary>
    /// The entry kinds the block knows. A kind is an open string: modules add their own, such as a ticket's
    /// <c>Reply</c>.
    /// </summary>
    public static class Kinds
    {
        /// <summary>
        /// A comment someone wrote.
        /// </summary>
        public const string Comment = "Comment";

        /// <summary>
        /// A note for the people who work on the record.
        /// </summary>
        public const string Note = "Note";

        /// <summary>
        /// Something that happened to the record, such as a change of status.
        /// </summary>
        public const string Event = "Event";
    }
}
