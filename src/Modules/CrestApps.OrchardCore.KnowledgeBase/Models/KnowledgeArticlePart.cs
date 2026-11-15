using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.KnowledgeBase.Models;

/// <summary>
/// What makes a content item a help article: who may read it, its category and summary, and what readers said about it.
/// </summary>
public sealed class KnowledgeArticlePart : ContentPart
{
    /// <summary>
    /// Gets or sets who may read the article.
    /// </summary>
    public KnowledgeArticleAudience Audience { get; set; }

    /// <summary>
    /// Gets or sets the article's category, as shown in the help center.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets the short summary shown in lists, searches and suggestions.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets how many readers said it helped.
    /// </summary>
    public int HelpfulCount { get; set; }

    /// <summary>
    /// Gets or sets how many readers said it didn't help.
    /// </summary>
    public int NotHelpfulCount { get; set; }
}

/// <summary>
/// Who may read an article.
/// </summary>
public enum KnowledgeArticleAudience
{
    /// <summary>
    /// Everyone, signed in or not.
    /// </summary>
    Public,

    /// <summary>
    /// Anyone signed in.
    /// </summary>
    SignedIn,

    /// <summary>
    /// Staff only: people with the View staff articles permission.
    /// </summary>
    Staff,
}
