namespace CrestApps.OrchardCore.Agreements;

/// <summary>
/// The constants of the Agreements module.
/// </summary>
public static class AgreementsConstants
{
    /// <summary>
    /// The record kind of an agreement, for references to one (such as the agreement time is charged to).
    /// </summary>
    public const string RecordKind = "Agreement";

    /// <summary>
    /// The notification types Agreements sends. They are stored on the notifications, so they never change.
    /// </summary>
    public static class NotificationTypes
    {
        /// <summary>An agreement passed 80% or 100% of its hours.</summary>
        public const string Hours = "AgreementHours";

        /// <summary>An agreement is about to end.</summary>
        public const string Ending = "AgreementEnding";
    }

    /// <summary>
    /// The YesSql collection agreements are kept in.
    /// </summary>
    public const string CollectionName = "Agreements";

    /// <summary>
    /// The name of the recipe step and deployment step.
    /// </summary>
    public const string RecipeStepName = "Agreements";

    /// <summary>
    /// The number series agreement numbers come from.
    /// </summary>
    public const string NumberSeries = "Agreements.Agreement";

    /// <summary>
    /// The first part of an agreement number, such as SA-00042.
    /// </summary>
    public const string NumberPrefix = "SA-";

    /// <summary>
    /// The shares of an agreement's hours at which observers are told, in percent.
    /// </summary>
    public static readonly IReadOnlyList<int> WarningPercents = [80, 100];

    /// <summary>
    /// The rounding an agreement may count each entry of time with, in minutes; 0 counts it as it is.
    /// </summary>
    public static readonly IReadOnlyList<int> RoundingMinutes = [0, 6, 15, 30];

    /// <summary>
    /// The feature identifiers.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// Agreements.
        /// </summary>
        public const string Agreements = "CrestApps.OrchardCore.Agreements";
    }
}
