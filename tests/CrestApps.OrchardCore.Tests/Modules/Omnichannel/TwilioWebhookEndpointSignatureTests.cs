using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Omnichannel.Sms.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

public sealed class TwilioWebhookEndpointSignatureTests
{
    private const string AuthToken = "test-auth-token";

    private const string WebhookPath = "/api/twilio/webhook/sms";

    [Fact]
    public void IsRequestValid_WhenSignatureMatches_ReturnsTrue()
    {
        // Arrange
        var form = new Dictionary<string, string>
        {
            ["Body"] = "hello",
            ["From"] = "+15550000001",
        };

        var context = CreateContext(queryString: string.Empty, form: form);
        context.Request.Headers["X-Twilio-Signature"] = Sign($"https://contact.example.com{WebhookPath}", form);

        // Act
        var isValid = TwilioWebhookEndpoint.IsRequestValid(context, AuthToken, siteBaseUrl: null, NullLogger.Instance);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsRequestValid_WhenSignatureIsForged_ReturnsFalse()
    {
        // Arrange
        var context = CreateContext(
            queryString: string.Empty,
            form: new Dictionary<string, string>
            {
                ["Body"] = "hello",
            });

        context.Request.Headers["X-Twilio-Signature"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("not-a-signature"));

        // Act
        var isValid = TwilioWebhookEndpoint.IsRequestValid(context, AuthToken, siteBaseUrl: null, NullLogger.Instance);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsRequestValid_WhenQueryStringIsTamperedWith_ReturnsFalse()
    {
        // Twilio signs the full URL through the end of the query string. When the query string is excluded from
        // the signed payload an attacker can replay a legitimately signed body against a different query string.

        // Arrange
        var form = new Dictionary<string, string>
        {
            ["Body"] = "hello",
        };

        var context = CreateContext(queryString: "?tenant=evil", form: form);
        context.Request.Headers["X-Twilio-Signature"] = Sign($"https://contact.example.com{WebhookPath}?tenant=trusted", form);

        // Act
        var isValid = TwilioWebhookEndpoint.IsRequestValid(context, AuthToken, siteBaseUrl: null, NullLogger.Instance);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsRequestValid_WhenQueryStringIsSigned_ReturnsTrue()
    {
        // Arrange
        var form = new Dictionary<string, string>
        {
            ["Body"] = "hello",
        };

        var context = CreateContext(queryString: "?tenant=trusted", form: form);
        context.Request.Headers["X-Twilio-Signature"] = Sign($"https://contact.example.com{WebhookPath}?tenant=trusted", form);

        // Act
        var isValid = TwilioWebhookEndpoint.IsRequestValid(context, AuthToken, siteBaseUrl: null, NullLogger.Instance);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsRequestValid_WhenSignatureHeaderIsMissing_ReturnsFalse()
    {
        // Arrange
        var context = CreateContext(
            queryString: string.Empty,
            form: new Dictionary<string, string>
            {
                ["Body"] = "hello",
            });

        // Act
        var isValid = TwilioWebhookEndpoint.IsRequestValid(context, AuthToken, siteBaseUrl: null, NullLogger.Instance);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsRequestValid_WhenAuthTokenIsMissing_ReturnsFalse()
    {
        // Arrange
        var form = new Dictionary<string, string>
        {
            ["Body"] = "hello",
        };

        var context = CreateContext(queryString: string.Empty, form: form);
        context.Request.Headers["X-Twilio-Signature"] = Sign($"https://contact.example.com{WebhookPath}", form);

        // Act
        var isValid = TwilioWebhookEndpoint.IsRequestValid(context, authToken: null, siteBaseUrl: null, NullLogger.Instance);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsRequestValid_BehindTlsTerminatingProxy_UsesConfiguredSiteBaseUrl()
    {
        // Twilio signs the public URL it was configured with. Behind a TLS-terminating proxy the request arrives
        // over plain HTTP on an internal host, so signing the request's own scheme and host rejects a genuine
        // delivery. The configured site base URL is the trusted source for the public URL.

        // Arrange
        var form = new Dictionary<string, string>
        {
            ["Body"] = "hello",
        };

        var context = CreateContext(queryString: string.Empty, form: form);
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("10.0.0.7", 8080);
        context.Request.Headers["X-Twilio-Signature"] = Sign($"https://contact.example.com{WebhookPath}", form);

        // Act
        var isValid = TwilioWebhookEndpoint.IsRequestValid(context, AuthToken, "https://contact.example.com", NullLogger.Instance);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsRequestValid_BehindTlsTerminatingProxy_WithoutConfiguredBaseUrl_ReturnsFalse()
    {
        // Guards the regression the previous assertion protects against: without the configured public URL the
        // internal hop is signed instead, and a genuine Twilio delivery is rejected.

        // Arrange
        var form = new Dictionary<string, string>
        {
            ["Body"] = "hello",
        };

        var context = CreateContext(queryString: string.Empty, form: form);
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("10.0.0.7", 8080);
        context.Request.Headers["X-Twilio-Signature"] = Sign($"https://contact.example.com{WebhookPath}", form);

        // Act
        var isValid = TwilioWebhookEndpoint.IsRequestValid(context, AuthToken, siteBaseUrl: null, NullLogger.Instance);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsRequestValid_WhenSiteBaseUrlHasNonDefaultPort_IncludesPortInSignedUrl()
    {
        // Arrange
        var form = new Dictionary<string, string>
        {
            ["Body"] = "hello",
        };

        var context = CreateContext(queryString: string.Empty, form: form);
        context.Request.Headers["X-Twilio-Signature"] = Sign($"https://contact.example.com:8443{WebhookPath}", form);

        // Act
        var isValid = TwilioWebhookEndpoint.IsRequestValid(context, AuthToken, "https://contact.example.com:8443", NullLogger.Instance);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsRequestValid_WhenSiteBaseUrlHasTrailingSlash_StillValidates()
    {
        // Arrange
        var form = new Dictionary<string, string>
        {
            ["Body"] = "hello",
        };

        var context = CreateContext(queryString: string.Empty, form: form);
        context.Request.Headers["X-Twilio-Signature"] = Sign($"https://contact.example.com{WebhookPath}", form);

        // Act
        var isValid = TwilioWebhookEndpoint.IsRequestValid(context, AuthToken, "https://contact.example.com/", NullLogger.Instance);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsRequestValid_WhenFormFieldIsTamperedWith_ReturnsFalse()
    {
        // Arrange
        var context = CreateContext(
            queryString: string.Empty,
            form: new Dictionary<string, string>
            {
                ["Body"] = "transfer $1",
            });

        context.Request.Headers["X-Twilio-Signature"] = Sign(
            $"https://contact.example.com{WebhookPath}",
            new Dictionary<string, string>
            {
                ["Body"] = "transfer $1000",
            });

        // Act
        var isValid = TwilioWebhookEndpoint.IsRequestValid(context, AuthToken, siteBaseUrl: null, NullLogger.Instance);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsRequestValid_WhenSiteBaseUrlHasBasePath_DoesNotDuplicateThePath()
    {
        // A tenant published under https://host/support receives requests whose path already carries the prefix
        // when the proxy forwards it. Appending the configured base path again would sign /support/support/...
        // and reject every genuine delivery.

        // Arrange
        var form = new Dictionary<string, string>
        {
            ["Body"] = "hello",
        };

        var context = CreateContext(queryString: string.Empty, form: form);
        context.Request.Path = $"/support{WebhookPath}";
        context.Request.Headers["X-Twilio-Signature"] = Sign($"https://contact.example.com/support{WebhookPath}", form);

        // Act
        var isValid = TwilioWebhookEndpoint.IsRequestValid(context, AuthToken, "https://contact.example.com/support", NullLogger.Instance);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsRequestValid_WhenSiteBaseUrlHasBasePathAndProxyStripsIt_StillValidates()
    {
        // The same deployment when the proxy strips the prefix before forwarding.

        // Arrange
        var form = new Dictionary<string, string>
        {
            ["Body"] = "hello",
        };

        var context = CreateContext(queryString: string.Empty, form: form);
        context.Request.Headers["X-Twilio-Signature"] = Sign($"https://contact.example.com/support{WebhookPath}", form);

        // Act
        var isValid = TwilioWebhookEndpoint.IsRequestValid(context, AuthToken, "https://contact.example.com/support", NullLogger.Instance);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsRequestValid_WhenTheApplicationIsHostedUnderAPathBase_StillValidates()
    {
        // Arrange
        var form = new Dictionary<string, string>
        {
            ["Body"] = "hello",
        };

        var context = CreateContext(queryString: string.Empty, form: form);
        context.Request.PathBase = new PathString("/support");
        context.Request.Headers["X-Twilio-Signature"] = Sign($"https://contact.example.com/support{WebhookPath}", form);

        // Act
        var isValid = TwilioWebhookEndpoint.IsRequestValid(context, AuthToken, "https://contact.example.com/support", NullLogger.Instance);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsRequestValid_WhenABasePathOnlySharesAPrefix_DoesNotTrimIt()
    {
        // "/support" must not be treated as a prefix of "/support-portal": trimming on a partial segment would
        // silently sign the wrong path.

        // Arrange
        var form = new Dictionary<string, string>
        {
            ["Body"] = "hello",
        };

        var context = CreateContext(queryString: string.Empty, form: form);
        context.Request.Path = $"/support-portal{WebhookPath}";
        context.Request.Headers["X-Twilio-Signature"] = Sign($"https://contact.example.com/support/support-portal{WebhookPath}", form);

        // Act
        var isValid = TwilioWebhookEndpoint.IsRequestValid(context, AuthToken, "https://contact.example.com/support", NullLogger.Instance);

        // Assert
        Assert.True(isValid);
    }

    private static DefaultHttpContext CreateContext(string queryString, Dictionary<string, string> form)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("contact.example.com");
        context.Request.Path = WebhookPath;
        context.Request.QueryString = new QueryString(queryString);
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(form.ToDictionary(pair => pair.Key, pair => new StringValues(pair.Value)));

        return context;
    }

    /// <summary>
    /// Produces a signature exactly as Twilio does: the absolute URL followed by every POST parameter ordered by
    /// name using an ordinal comparison, hashed with HMAC-SHA1 keyed on the auth token, then Base64 encoded.
    /// </summary>
    /// <param name="url">The absolute URL Twilio was configured with.</param>
    /// <param name="form">The POST parameters included in the delivery.</param>
    private static string Sign(string url, Dictionary<string, string> form)
    {
        var builder = new StringBuilder(url);

        foreach (var key in form.Keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            builder.Append(key).Append(form[key]);
        }

#pragma warning disable CA5350 // Twilio mandates HMAC-SHA1 for request signatures.
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(AuthToken));
#pragma warning restore CA5350

        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
    }
}
