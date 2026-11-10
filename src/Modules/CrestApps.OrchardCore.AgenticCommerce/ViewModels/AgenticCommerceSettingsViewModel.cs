namespace CrestApps.OrchardCore.AgenticCommerce.ViewModels;

/// <summary>
/// The Agentic commerce settings page.
/// </summary>
public class AgenticCommerceSettingsViewModel
{
    public int CheckoutLifetimeHours { get; set; }

    public int MaxOpenCheckoutsPerPlatform { get; set; }

    public bool AllowUnregisteredUcpPlatforms { get; set; }

    public bool AllowEmbeddedCheckout { get; set; }

    public string StripeBusinessProfileId { get; set; }

    public bool EnableGooglePay { get; set; }

    public string GooglePayMerchantId { get; set; }

    public string GooglePayMerchantName { get; set; }

    public bool GooglePayTestEnvironment { get; set; }

    public bool EnableDelegatedAuthentication { get; set; }

    public string AcquirerBin { get; set; }

    public string AcquirerCountry { get; set; }

    public string AcquirerMerchantId { get; set; }

    public string AuthenticationMerchantName { get; set; }

    public string ThreeDSecureRequestorId { get; set; }
}
