namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Carries the time a survey's draft last changed through a link or form as ticks, so an action that replaces the draft
/// can refuse a draft another author changed after the page was shown.
/// </summary>
internal static class SurveyDraftStamp
{
    /// <summary>
    /// Gets the stamp of a draft time.
    /// </summary>
    /// <param name="draftModifiedUtc">The time the draft last changed.</param>
    /// <returns>The ticks, or <see langword="null"/>.</returns>
    public static long? From(DateTime? draftModifiedUtc)
        => draftModifiedUtc?.Ticks;

    /// <summary>
    /// Gets the draft time of a stamp.
    /// </summary>
    /// <param name="stamp">The ticks.</param>
    /// <returns>The time, or <see langword="null"/> for no or an invalid stamp.</returns>
    public static DateTime? ToUtc(long? stamp)
        => stamp is { } ticks && ticks > 0 && ticks <= DateTime.MaxValue.Ticks ? new DateTime(ticks, DateTimeKind.Utc) : null;
}
