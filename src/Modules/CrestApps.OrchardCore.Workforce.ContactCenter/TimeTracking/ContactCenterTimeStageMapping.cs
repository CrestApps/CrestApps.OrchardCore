namespace CrestApps.OrchardCore.Workforce.ContactCenter.TimeTracking;

/// <summary>
/// Applies chosen stages to the stage map of agent states or reasons, as the Contact Center Stages page saves them.
/// </summary>
internal static class ContactCenterTimeStageMapping
{
    /// <summary>
    /// Sets each row's stage when it is an existing stage, and stops tracking the row's state or reason otherwise.
    /// </summary>
    /// <param name="target">The map of a state's or reason's name to its stage's ID.</param>
    /// <param name="rows">The rows: a state's or reason's name, and the chosen stage's ID or nothing.</param>
    /// <param name="stageIds">The IDs of the existing stages.</param>
    public static void Apply(Dictionary<string, string> target, IEnumerable<(string Key, string StageId)> rows, IReadOnlySet<string> stageIds)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(stageIds);

        foreach (var (key, stageId) in rows ?? [])
        {
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(stageId) && stageIds.Contains(stageId))
            {
                target[key] = stageId;
            }
            else
            {
                target.Remove(key);
            }
        }
    }
}
