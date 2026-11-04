namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// One break or activity inside a shift, by time of day.
/// </summary>
/// <param name="ActivityCodeId">The activity code.</param>
/// <param name="Start">When it starts.</param>
/// <param name="End">When it ends; earlier than the start when it ends after midnight.</param>
internal readonly record struct SegmentLine(string ActivityCodeId, TimeOnly Start, TimeOnly End);
