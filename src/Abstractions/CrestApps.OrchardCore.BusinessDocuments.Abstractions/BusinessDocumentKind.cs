using System.Text.Json.Serialization;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.BusinessDocuments;

/// <summary>
/// A kind of business document a module prints, such as receipts or invoices, registered with
/// <see cref="BusinessDocumentServiceCollectionExtensions.AddBusinessDocumentKind"/>.
/// </summary>
public sealed class BusinessDocumentKind
{
    /// <summary>
    /// Gets or sets the name of the kind, such as <c>Invoice</c>. It is the value of
    /// <see cref="Models.BusinessDocument.Kind"/> and of the <c>BusinessDocument__{Kind}</c> shape alternate.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the title shown for the kind, such as on the template editor.
    /// </summary>
    [JsonIgnore]
    public LocalizationSource Title { get; set; }
}
