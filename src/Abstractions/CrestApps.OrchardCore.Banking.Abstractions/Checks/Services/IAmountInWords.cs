namespace CrestApps.OrchardCore.Banking.Checks.Services;

/// <summary>
/// Writes an amount in words for the legal line of a check, in one language. Register one per language with
/// <see cref="ChecksServiceCollectionExtensions.AddAmountInWords{TWriter}"/>; English is built in.
/// </summary>
public interface IAmountInWords
{
    /// <summary>
    /// Gets the two-letter ISO language code this writer writes, such as <c>en</c>.
    /// </summary>
    string Language { get; }

    /// <summary>
    /// Writes an amount, with the fraction in figures, such as "One thousand two hundred thirty-four and 56/100", and
    /// the currency's name when one is given, such as "... and 56/100 Dollars".
    /// </summary>
    /// <param name="amount">The amount; zero or more.</param>
    /// <param name="decimals">The number of decimals of the currency, from <c>CurrencyScale</c>.</param>
    /// <param name="currency">The ISO currency code, or <see langword="null"/> to write no currency.</param>
    /// <returns>The words.</returns>
    string Write(decimal amount, int decimals = 2, string currency = null);
}
