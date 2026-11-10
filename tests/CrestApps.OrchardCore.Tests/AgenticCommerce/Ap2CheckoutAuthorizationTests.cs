using System.Buffers.Text;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Ap2;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// The UCP AP2 merchant authorization signs the JCS form of the checkout without <c>ap2</c>, bridges to an AP2
/// checkout JWT, and checkout receipts carry the AP2 receipt claims.
/// </summary>
public sealed class Ap2CheckoutAuthorizationTests
{
    private readonly EcJsonWebKey _merchantKey = Ap2TestMandates.NewKey("merchant-key-1");

    [Fact]
    public void CreateMerchantAuthorization_ThenVerify_IgnoresTheAp2MemberAndMemberOrder()
    {
        // Arrange
        var checkout = Ap2TestMandates.SampleCheckout();
        var publicKey = Ap2TestMandates.PublicOf(_merchantKey);

        // Act
        var authorization = Ap2CheckoutAuthorization.CreateMerchantAuthorization(checkout, _merchantKey);
        var reordered = new JsonObject();

        foreach (var property in checkout.Reverse())
        {
            reordered[property.Key] = property.Value?.DeepClone();
        }

        reordered["ap2"] = new JsonObject { ["merchant_authorization"] = authorization };

        // Assert
        Assert.Matches("^[A-Za-z0-9_-]+\\.\\.[A-Za-z0-9_-]+$", authorization);
        Assert.Equal("{\"alg\":\"ES256\",\"kid\":\"merchant-key-1\"}", Encoding.UTF8.GetString(Base64Url.DecodeFromChars(authorization.Split('.')[0])));
        Assert.True(Ap2CheckoutAuthorization.VerifyMerchantAuthorization(reordered, authorization, kid => kid == "merchant-key-1" ? publicKey : null));
    }

    [Fact]
    public void VerifyMerchantAuthorization_ChangedCheckoutOrUnknownKey_Fails()
    {
        // Arrange
        var checkout = Ap2TestMandates.SampleCheckout();
        var authorization = Ap2CheckoutAuthorization.CreateMerchantAuthorization(checkout, _merchantKey);
        var changed = Ap2TestMandates.SampleCheckout(quantity: 3);
        var publicKey = Ap2TestMandates.PublicOf(_merchantKey);

        // Act and Assert
        Assert.False(Ap2CheckoutAuthorization.VerifyMerchantAuthorization(changed, authorization, _ => publicKey));
        Assert.False(Ap2CheckoutAuthorization.VerifyMerchantAuthorization(checkout, authorization, _ => null));
        Assert.False(Ap2CheckoutAuthorization.VerifyMerchantAuthorization(checkout, authorization, _ => Ap2TestMandates.PublicOf(Ap2TestMandates.NewKey("merchant-key-1"))));
    }

    [Fact]
    public void ToCompactCheckoutJwt_IsAValidJwsWithTheCanonicalCheckout()
    {
        // Arrange
        var checkout = Ap2TestMandates.SampleCheckout();
        checkout["ap2"] = new JsonObject { ["merchant_authorization"] = "x..y" };
        var authorization = Ap2CheckoutAuthorization.CreateMerchantAuthorization(checkout, _merchantKey);
        var publicKey = Ap2TestMandates.PublicOf(_merchantKey);

        // Act
        var checkoutJwt = Ap2CheckoutAuthorization.ToCompactCheckoutJwt(checkout, authorization);
        var verified = JwtEs256.TryVerify(checkoutJwt, _ => publicKey, out _, out var claims);

        // Assert
        Assert.True(verified);
        Assert.False(claims.ContainsKey("ap2"));
        Assert.Equal("chk_test", claims["id"].GetValue<string>());
        Assert.Equal(JsonCanonicalizer.Canonicalize(Ap2TestMandates.SampleCheckout()), Encoding.UTF8.GetString(Base64Url.DecodeFromChars(checkoutJwt.Split('.')[1])));
    }

    [Fact]
    public void CreateCheckoutReceipt_SuccessAndError_CarryTheReceiptClaims()
    {
        // Arrange
        var publicKey = Ap2TestMandates.PublicOf(_merchantKey);
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(Ap2TestMandates.Now);

        // Act
        var success = Ap2Receipts.CreateCheckoutReceipt("ref-hash", Ap2Receipts.StatusSuccess, "https://store.example", issuedAt, _merchantKey, null, null, "order-1");
        var error = Ap2Receipts.CreateCheckoutReceipt("ref-hash", Ap2Receipts.StatusError, "https://store.example", issuedAt, _merchantKey, "invalid_mandate");
        Assert.True(JwtEs256.TryVerify(success, _ => publicKey, out var successHeader, out var successClaims));
        Assert.True(JwtEs256.TryVerify(error, _ => publicKey, out _, out var errorClaims));

        // Assert
        Assert.Equal("merchant-key-1", successHeader["kid"].GetValue<string>());
        Assert.Equal("Success", successClaims["status"].GetValue<string>());
        Assert.Equal("https://store.example", successClaims["iss"].GetValue<string>());
        Assert.Equal(Ap2TestMandates.Now, successClaims["iat"].GetValue<long>());
        Assert.Equal("ref-hash", successClaims["reference"].GetValue<string>());
        Assert.Equal("order-1", successClaims["order_id"].GetValue<string>());
        Assert.Equal("Error", errorClaims["status"].GetValue<string>());
        Assert.Equal("invalid_mandate", errorClaims["error"].GetValue<string>());
        Assert.False(errorClaims.ContainsKey("order_id"));
    }
}
