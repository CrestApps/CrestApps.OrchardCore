namespace CrestApps.OrchardCore.Banking.Services;

/// <summary>
/// Checks the routing and account numbers of one country's banks. Register one per country with
/// <see cref="BankingServiceCollectionExtensions.AddBankRoutingValidator{TValidator}"/>; a country without one gets
/// only the checks every country shares (letters and digits, a length a bank uses).
/// </summary>
public interface IBankRoutingValidator
{
    /// <summary>
    /// Gets the two-letter ISO country code this validator checks.
    /// </summary>
    string Country { get; }

    /// <summary>
    /// Checks a routing number and writes it the way it is stored, without spaces or dashes.
    /// </summary>
    /// <param name="value">The number as entered.</param>
    /// <param name="normalized">The number as it is stored, when it is valid.</param>
    /// <returns><see langword="true"/> when the number is valid.</returns>
    bool TryNormalizeRoutingNumber(string value, out string normalized);

    /// <summary>
    /// Checks an account number and writes it the way it is stored, without spaces or dashes.
    /// </summary>
    /// <param name="value">The number as entered.</param>
    /// <param name="normalized">The number as it is stored, when it is valid.</param>
    /// <returns><see langword="true"/> when the number is valid.</returns>
    bool TryNormalizeAccountNumber(string value, out string normalized);
}
