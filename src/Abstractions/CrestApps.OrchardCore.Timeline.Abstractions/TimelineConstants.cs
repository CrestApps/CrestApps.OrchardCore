namespace CrestApps.OrchardCore.Timeline;

/// <summary>
/// Names shared by the Timeline building block and the modules that build on it.
/// </summary>
public static class TimelineConstants
{
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
    /// The record kind of a content item's timeline, such as an account, a contact or a lead; the record id is the
    /// content item id.
    /// </summary>
    public const string ContentItemKind = "Content";

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
