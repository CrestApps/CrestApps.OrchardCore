namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public sealed class PaymentCardInfo
{
    public string Brand { get; set; }

    public string Country { get; set; }

    public string LastFour { get; set; }

    public long? ExpirationMonth { get; set; }

    public long? ExpirationYear { get; set; }

    public string Fingerprint { get; set; }

    public string Issuer { get; set; }
}
