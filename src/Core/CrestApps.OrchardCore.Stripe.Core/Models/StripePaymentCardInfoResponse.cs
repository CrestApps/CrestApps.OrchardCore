namespace CrestApps.OrchardCore.Stripe.Core.Models;

public sealed class StripePaymentCardInfoResponse
{
    /// <summary>
    /// Card brand. Can be <c>amex</c>, <c>diners</c>, <c>discover</c>, <c>eftpos_au</c>,
    /// <c>jcb</c>, <c>mastercard</c>, <c>unionpay</c>, <c>visa</c>, or <c>unknown</c>.
    /// </summary>
    public string Brand { get; set; }

    /// <summary>
    /// Two-letter ISO code representing the country of the card. You could use this attribute
    /// to get a sense of the international breakdown of cards you've collected.
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// The brand to use when displaying the card, this accounts for customer's brand choice on
    /// dual-branded cards. Can be <c>american_express</c>, <c>cartes_bancaires</c>,
    /// <c>diners_club</c>, <c>discover</c>, <c>eftpos_australia</c>, <c>interac</c>,
    /// <c>jcb</c>, <c>mastercard</c>, <c>union_pay</c>, <c>visa</c>, or <c>other</c> and may
    /// contain more values in the future.
    /// </summary>
    public string DisplayBrand { get; set; }

    /// <summary>
    /// Two-digit number representing the card's expiration month.
    /// </summary>
    public long ExpirationMonth { get; set; }

    /// <summary>
    /// Four-digit number representing the card's expiration year.
    /// </summary>
    public long ExpirationYear { get; set; }

    /// <summary>
    /// Uniquely identifies this particular card number. You can use this attribute to check
    /// whether two customers who’ve signed up with you are using the same card number, for
    /// example. For payment methods that tokenize card information (Apple Pay, Google Pay), the
    /// tokenized number might be provided instead of the underlying card number.
    ///
    /// <em>As of May 1, 2021, card fingerprint in India for Connect changed to allow two
    /// fingerprints for the same card---one for India and one for the rest of the world.</em>.
    /// </summary>
    public string Fingerprint { get; set; }

    /// <summary>
    /// The name of the card's issuing bank. (For internal use only and not typically available
    /// in standard API requests.).
    /// </summary>
    public string Issuer { get; set; }

    /// <summary>
    /// The last four digits of the card.
    /// </summary>
    public string LastFour { get; set; }
}
