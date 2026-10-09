namespace CrestApps.OrchardCore.Reports.Contents;

/// <summary>
/// The stable names of the content item metadata fields every content type data set has. Report designs store these
/// names. Part and field values are named <c>{PartName}.{FieldName}</c>, with an optional <c>.{Suffix}</c>.
/// </summary>
public static class ContentReportFieldNames
{
    /// <summary>
    /// The content item ID, an identifier.
    /// </summary>
    public const string ContentItemId = "ContentItemId";

    /// <summary>
    /// The ID of the published version of the content item.
    /// </summary>
    public const string ContentItemVersionId = "ContentItemVersionId";

    /// <summary>
    /// The display text of the content item.
    /// </summary>
    public const string DisplayText = "DisplayText";

    /// <summary>
    /// The technical name of the content type.
    /// </summary>
    public const string ContentType = "ContentType";

    /// <summary>
    /// The ID of the user who owns the content item, an identifier.
    /// </summary>
    public const string Owner = "Owner";

    /// <summary>
    /// The user name of the last person who edited the content item.
    /// </summary>
    public const string Author = "Author";

    /// <summary>
    /// When the content item was created, in UTC.
    /// </summary>
    public const string CreatedUtc = "CreatedUtc";

    /// <summary>
    /// When the content item was last modified, in UTC.
    /// </summary>
    public const string ModifiedUtc = "ModifiedUtc";

    /// <summary>
    /// When the content item was published, in UTC.
    /// </summary>
    public const string PublishedUtc = "PublishedUtc";

    /// <summary>
    /// Whether the content item is published.
    /// </summary>
    public const string Published = "Published";
}
