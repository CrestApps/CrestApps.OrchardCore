namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// The 3-D Secure directory servers an agent can authenticate a buyer with (<see cref="AgentAuthenticationMetadata.DirectoryServer"/>).
/// </summary>
public static class AgentDirectoryServers
{
    /// <summary>
    /// Visa.
    /// </summary>
    public const string Visa = "visa";

    /// <summary>
    /// Mastercard.
    /// </summary>
    public const string Mastercard = "mastercard";

    /// <summary>
    /// American Express.
    /// </summary>
    public const string AmericanExpress = "american_express";

    /// <summary>
    /// Returns the directory server of a card brand, as payment providers and agents name it (<c>visa</c>,
    /// <c>mastercard</c>, <c>amex</c>…), or <see langword="null"/> for a brand without one.
    /// </summary>
    /// <param name="brand">The card brand.</param>
    public static string FromBrand(string brand)
        => brand?.Trim().ToLowerInvariant() switch
        {
            "visa" => Visa,
            "mastercard" or "master_card" or "mc" => Mastercard,
            "amex" or "american_express" or "americanexpress" or "american express" => AmericanExpress,
            _ => null,
        };
}
