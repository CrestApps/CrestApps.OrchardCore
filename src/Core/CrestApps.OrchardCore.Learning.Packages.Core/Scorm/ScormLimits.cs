namespace CrestApps.OrchardCore.Learning.Packages.Core.Scorm;

/// <summary>
/// The most entries each list of the runtime data takes. They are the smallest permitted maximums of SCORM 2004 4th
/// Edition (which SCORM 1.2 content also stays within), so conforming content fits, while a learner cannot grow the
/// runtime state, which every runtime call loads and writes, without limit.
/// </summary>
public static class ScormLimits
{
    /// <summary>The most interactions.</summary>
    public const int Interactions = 250;

    /// <summary>The most objectives.</summary>
    public const int Objectives = 100;

    /// <summary>The most objective ids of an interaction.</summary>
    public const int InteractionObjectives = 10;

    /// <summary>The most correct response patterns of an interaction.</summary>
    public const int CorrectResponses = 10;

    /// <summary>The most comments from the learner (SCORM 2004).</summary>
    public const int CommentsFromLearner = 250;

    /// <summary>
    /// The largest runtime state, serialized, in bytes. A change that would make the state larger is refused whole.
    /// </summary>
    public const int MaxStateBytes = 1024 * 1024;

    /// <summary>
    /// Tells whether a list may take the entry at an index: an existing one, or the next one while the list is under
    /// its limit.
    /// </summary>
    /// <param name="index">The index written.</param>
    /// <param name="count">The entries the list has.</param>
    /// <param name="limit">The most entries it takes.</param>
    /// <returns><see langword="true"/> when the write may go on.</returns>
    public static bool MayAdd(int index, int count, int limit)
        => index < count || (index == count && count < limit);
}
