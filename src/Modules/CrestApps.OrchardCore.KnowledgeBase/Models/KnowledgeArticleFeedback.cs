namespace CrestApps.OrchardCore.KnowledgeBase.Models;

/// <summary>
/// A reader's answer to "Was this helpful?" on an article: whether it helped, what was missing when it didn't, when, and
/// who answered when they were signed in. Kept in the Knowledge Base's own collection.
/// </summary>
public sealed class KnowledgeArticleFeedback
{
    /// <summary>
    /// Gets or sets the YesSql document id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the answer's unique id.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the article.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the version of the article the reader read.
    /// </summary>
    public string ContentItemVersionId { get; set; }

    /// <summary>
    /// Gets or sets the article's title when it was answered.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the article's category when it was answered, a term of the categories' taxonomy.
    /// </summary>
    public string CategoryId { get; set; }

    /// <summary>
    /// Gets or sets whether the article helped.
    /// </summary>
    public bool Helpful { get; set; }

    /// <summary>
    /// Gets or sets what the reader said was missing, after answering No.
    /// </summary>
    public string Comment { get; set; }

    /// <summary>
    /// Gets or sets when the reader answered.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the signed-in reader, or <see langword="null"/> for a visitor.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the signed-in reader's user name.
    /// </summary>
    public string UserName { get; set; }
}
