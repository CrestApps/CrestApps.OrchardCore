using Microsoft.Extensions.Localization;

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
    /// Gets or sets the localized display name shown in the report and its source filter.
    /// </summary>
    public LocalizedString DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the optional localized description of the source.
    /// </summary>
    public LocalizedString Description { get; set; }
}
