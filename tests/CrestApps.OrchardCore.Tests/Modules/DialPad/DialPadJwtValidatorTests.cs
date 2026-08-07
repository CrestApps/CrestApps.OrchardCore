using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.DialPad.Services;

namespace CrestApps.OrchardCore.Tests.Modules.DialPad;

public sealed class DialPadJwtValidatorTests
{
    private const string Payload = "{\"call_id\":\"c1\",\"state\":\"ringing\"}";

    [Fact]
    public void TryValidateAndExtract_WithValidSignedJwt_ExtractsPayload()
    {
        // Arrange
        var jwt = CreateJwt(Payload, "shhh");

        // Act
        var valid = DialPadJwtValidator.TryValidateAndExtract(jwt, "shhh", out var payload);

        // Assert
        Assert.True(valid);
        Assert.Equal(Payload, payload);
    }

    [Fact]
    public void TryValidateAndExtract_WithWrongSecret_Fails()
    {
        // Arrange
        var jwt = CreateJwt(Payload, "shhh");

        // Act
        var valid = DialPadJwtValidator.TryValidateAndExtract(jwt, "different", out _);

        // Assert
        Assert.False(valid);
    }

    [Fact]
    public void TryValidateAndExtract_WithTamperedPayload_Fails()
    {
        // Arrange
        var jwt = CreateJwt(Payload, "shhh");
        var segments = jwt.Split('.');
        var tampered = $"{segments[0]}.{Base64Url(Encoding.UTF8.GetBytes("{\"call_id\":\"evil\"}"))}.{segments[2]}";

        // Act
        var valid = DialPadJwtValidator.TryValidateAndExtract(tampered, "shhh", out _);

        // Assert
        Assert.False(valid);
    }

    [Fact]
    public void TryValidateAndExtract_WithNoSecretAndRawJson_ExtractsBody()
    {
        // Act
        var valid = DialPadJwtValidator.TryValidateAndExtract(Payload, signingSecret: null, out var payload);

        // Assert
        Assert.True(valid);
        Assert.Equal(Payload, payload);
    }

    [Fact]
    public void TryValidateAndExtract_WithRawJsonButSecretConfigured_Fails()
    {
        // Act
        var valid = DialPadJwtValidator.TryValidateAndExtract(Payload, "shhh", out _);

        // Assert
        Assert.False(valid);
    }

    private static string CreateJwt(string payloadJson, string secret)
    {
        var header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var payload = Base64Url(Encoding.UTF8.GetBytes(payloadJson));
        var signingInput = $"{header}.{payload}";
        var signature = Base64Url(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signingInput)));

        return $"{signingInput}.{signature}";
    }

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
