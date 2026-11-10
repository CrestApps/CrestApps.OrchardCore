namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// What an agent needs to run 3-D Secure with the buyer itself: the store's acquirer and the card network's directory
/// server.
/// </summary>
public sealed class AgentAuthenticationMetadata
{
    /// <summary>
    /// Gets or sets the acquirer's BIN.
    /// </summary>
    public string AcquirerBin { get; set; }

    /// <summary>
    /// Gets or sets the acquirer's country, as an ISO 3166-1 alpha-2 code.
    /// </summary>
    public string AcquirerCountry { get; set; }

    /// <summary>
    /// Gets or sets the store's merchant ID at the acquirer.
    /// </summary>
    public string AcquirerMerchantId { get; set; }

    /// <summary>
    /// Gets or sets the merchant name the buyer's bank shows.
    /// </summary>
    public string MerchantName { get; set; }

    /// <summary>
    /// Gets or sets the 3-D Secure requestor ID, when the store has one.
    /// </summary>
    public string RequestorId { get; set; }

    /// <summary>
    /// Gets or sets the card network's directory server, one of <see cref="AgentDirectoryServers"/>.
    /// </summary>
    public string DirectoryServer { get; set; }
}
