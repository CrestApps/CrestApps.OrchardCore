using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout.Services;

namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// How the agent pays: a credential from one of the payment handlers the store advertised.
/// </summary>
public sealed class AgentPayment
{
    /// <summary>
    /// Gets or sets the id of the payment handler the credential is for.
    /// </summary>
    public string HandlerId { get; set; }

    /// <summary>
    /// Gets or sets the instrument's type, such as <c>card</c>.
    /// </summary>
    public string InstrumentType { get; set; }

    /// <summary>
    /// Gets or sets the credential's type, as the protocol names it.
    /// </summary>
    public string CredentialType { get; set; }

    /// <summary>
    /// Gets or sets the credential itself, such as a token. It is never stored or logged.
    /// </summary>
    public string Credential { get; set; }

    /// <summary>
    /// Gets or sets the card's brand, as the agent shows it.
    /// </summary>
    public string Brand { get; set; }

    /// <summary>
    /// Gets or sets the card's last digits, as the agent shows them.
    /// </summary>
    public string LastDigits { get; set; }

    /// <summary>
    /// Gets or sets the billing address.
    /// </summary>
    public Address BillingAddress { get; set; }

    /// <summary>
    /// Gets or sets the result of the 3-D Secure authentication the agent ran with the buyer, when the checkout asked
    /// for one (<see cref="AgentCheckoutStatus.AuthenticationRequired"/>). It is never stored or logged.
    /// </summary>
    public ThreeDSecureResult Authentication { get; set; }
}
