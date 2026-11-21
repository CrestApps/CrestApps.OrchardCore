namespace CrestApps.OrchardCore.ContentTransfer;

/// <summary>
/// The names of the columns the import itself reads, rather than a part or field handler.
/// </summary>
public static class ContentTransferColumns
{
    /// <summary>
    /// The column that gives a row its content type, read only when an <see cref="IContentImportRowTypeProvider"/>
    /// lets the import hold several types.
    /// </summary>
    public const string ContentType = "ContentType";
}
