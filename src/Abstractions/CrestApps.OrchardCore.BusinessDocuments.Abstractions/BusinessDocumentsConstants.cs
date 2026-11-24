namespace CrestApps.OrchardCore.BusinessDocuments;

/// <summary>
/// The constants of the business documents building block.
/// </summary>
public static class BusinessDocumentsConstants
{
    /// <summary>
    /// The name of the themeable shape that shows a business document. The shape takes the document as its
    /// <c>Document</c> property, and a <c>BusinessDocument__{Kind}</c> alternate is added for the document's kind.
    /// </summary>
    public const string ShapeType = "BusinessDocument";

    /// <summary>
    /// The group of the site settings page that lists the number series.
    /// </summary>
    public const string NumberSeriesSettingsGroupId = "number-series";

    /// <summary>
    /// The feature ids of the business documents module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The business document model, document templates, the HTML renderer, the shape and the number series
        /// settings.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.BusinessDocuments";

        /// <summary>
        /// The PDF renderer of business documents.
        /// </summary>
        public const string Pdf = "CrestApps.OrchardCore.BusinessDocuments.Pdf";
    }
}
