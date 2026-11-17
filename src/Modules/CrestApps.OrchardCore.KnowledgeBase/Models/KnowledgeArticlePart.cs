using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.KnowledgeBase.Models;

/// <summary>
/// What makes a content item a help article: its summary and what readers said about it.
/// </summary>
/// <remarks>Who may read an article is the Content Access Control feature's <c>RolePickerPart</c> on the type.</remarks>
public sealed class KnowledgeArticlePart : ContentPart
{
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
