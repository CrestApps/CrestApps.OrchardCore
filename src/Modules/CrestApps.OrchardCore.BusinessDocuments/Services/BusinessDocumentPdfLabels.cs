namespace CrestApps.OrchardCore.BusinessDocuments.Services;

/// <summary>
/// The localized text the PDF renderer prints where the document leaves a label empty.
/// </summary>
/// <param name="RecipientTitle">The title of the recipient block, such as "Bill to".</param>
/// <param name="ShipToTitle">The title of the ship-to block, such as "Ship to".</param>
/// <param name="TaxId">The tax identification number of a party, with <c>{0}</c> for the number.</param>
/// <param name="PageNumbers">The page numbers, with <c>{0}</c> for the page and <c>{1}</c> for the number of pages.</param>
internal sealed record BusinessDocumentPdfLabels(string RecipientTitle, string ShipToTitle, string TaxId, string PageNumbers);
