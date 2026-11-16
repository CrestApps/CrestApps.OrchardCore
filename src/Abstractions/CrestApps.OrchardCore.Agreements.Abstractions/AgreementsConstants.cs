namespace CrestApps.OrchardCore.Agreements;

/// <summary>
/// The constants of the Agreements module.
/// </summary>
public static class AgreementsConstants
{
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
