namespace CrestApps.OrchardCore.Checks.Services;

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
    /// Writes an amount, with the fraction in figures, such as "One thousand two hundred thirty-four and 56/100".
    /// </summary>
    /// <param name="amount">The amount; zero or more.</param>
    /// <param name="decimals">The number of decimals of the currency, from <c>CurrencyScale</c>.</param>
    /// <returns>The words.</returns>
    string Write(decimal amount, int decimals = 2);
}
