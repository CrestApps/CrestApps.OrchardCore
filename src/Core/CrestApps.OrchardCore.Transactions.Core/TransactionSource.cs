using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Transactions.Core;

/// <summary>
/// Describes a registered transaction source (the origin/provider that creates a transaction). A source lets
/// the administration report present and filter transactions by a friendly, localizable name instead of the
/// raw <see cref="Models.Transaction.Source"/> key.
/// </summary>
public sealed class TransactionSource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionSource"/> class.
    /// </summary>
    /// <param name="name">The technical source key stored on <see cref="Models.Transaction.Source"/>.</param>
    public TransactionSource(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Gets the technical source key stored on <see cref="Models.Transaction.Source"/>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets the display name shown in the report and its source filter, translated in the language of the
    /// request that shows it.
    /// </summary>
    public LocalizationSource DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the optional description of the source, translated in the language of the request that shows it.
    /// </summary>
    public LocalizationSource Description { get; set; }
}
