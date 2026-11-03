namespace CrestApps.OrchardCore.NLWeb.AI;

/// <summary>
/// The prompt templates the NLWeb module ships, by the id they are discovered under.
/// </summary>
/// <remarks>
/// A template's id is its file name without the extension, and the files live in <c>Templates/Prompts</c> in the
/// module, so a site can override them like any other prompt template.
/// </remarks>
public static class NLWebTemplateIds
{
    /// <summary>
    /// Rewrites a follow-up question into a question that stands on its own.
    /// </summary>
    public const string Decontextualize = "nlweb-decontextualize";

    /// <summary>
    /// Scores how well each result answers the question.
    /// </summary>
    public const string Rank = "nlweb-rank";

    /// <summary>
    /// Summarizes the results.
    /// </summary>
    public const string Summarize = "nlweb-summarize";

    /// <summary>
    /// Answers the question from the results.
    /// </summary>
    public const string Generate = "nlweb-generate";
}
