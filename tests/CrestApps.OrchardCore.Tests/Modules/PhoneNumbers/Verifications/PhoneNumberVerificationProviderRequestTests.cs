using System.Net;
using System.Text;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Models;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.PhoneNumbers.Verifications;

public sealed class PhoneNumberVerificationProviderRequestTests
{
    private static readonly DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AbstractApi_VerifyAsync_UsesApiKeyQueryParameterWithoutAuthorizationHeader()
    {
        // Arrange
        const string payload = """
        {
            "phone": "14152007986",
            "valid": true,
            "format": {
                "international": "+14152007986",
                "local": "(415) 200-7986"
            },
            "country": {
                "code": "US",
                "name": "United States",
                "prefix": "+1"
            },
            "type": "mobile",
            "carrier": "T-Mobile USA, Inc."
        }
        """;

        var handler = new RecordingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        });

        var provider = CreateAbstractApiProvider(handler, new AbstractApiPhoneNumberVerificationSettings
        {
            ProtectedApiKey = Protect("valid-api-key"),
            AuthenticationType = PhoneNumberVerificationAuthenticationType.Basic,
            Username = "stale-user",
            ProtectedPassword = Protect("stale-password"),
        });

        // Act
        var result = await provider.VerifyAsync("+14152007986", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PhoneNumberVerificationStatus.Verified, result.Status);
        Assert.NotNull(handler.Request);
        Assert.Null(handler.Request.Headers.Authorization);
        Assert.Contains("api_key=valid-api-key", handler.Request.RequestUri.Query);
        Assert.Contains("phone=%2B14152007986", handler.Request.RequestUri.Query);
    }

    [Fact]
    public async Task AbstractApi_VerifyAsync_TrimsUnprotectedApiKeyBeforeSending()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(CreateValidAbstractApiPayload(), Encoding.UTF8, "application/json"),
        });

        var provider = CreateAbstractApiProvider(handler, new AbstractApiPhoneNumberVerificationSettings
        {
            ProtectedApiKey = Protect(" valid-api-key \r\n"),
        });

        // Act
        var result = await provider.VerifyAsync("+14152007986", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PhoneNumberVerificationStatus.Verified, result.Status);
        Assert.NotNull(handler.Request);
        Assert.Contains("api_key=valid-api-key", handler.Request.RequestUri.Query);
        Assert.DoesNotContain("%20valid-api-key", handler.Request.RequestUri.Query);
        Assert.DoesNotContain("valid-api-key%20", handler.Request.RequestUri.Query);
    }

    [Fact]
    public async Task AbstractApi_VerifyAsync_WhenApiKeyCannotBeResolved_ReturnsFailedWithoutCallingProvider()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var provider = CreateAbstractApiProvider(handler, new AbstractApiPhoneNumberVerificationSettings());

        // Act
        var result = await provider.VerifyAsync("+14152007986", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PhoneNumberVerificationStatus.Failed, result.Status);
        Assert.Equal("AbstractAPI API key is not configured.", result.ErrorMessage);
        Assert.Null(handler.Request);
    }

    private static AbstractApiPhoneNumberVerificationProvider CreateAbstractApiProvider(
        HttpMessageHandler handler,
        AbstractApiPhoneNumberVerificationSettings settings)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(factory => factory.CreateClient(nameof(AbstractApiPhoneNumberVerificationProvider)))
            .Returns(new HttpClient(handler));

        var site = new Mock<ISite>();
        site.Setup(site => site.GetOrCreate<AbstractApiPhoneNumberVerificationSettings>())
            .Returns(settings);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync())
            .ReturnsAsync(site.Object);

        var dataProtectionProvider = new FakeDataProtectionProvider();
        var clock = new Mock<IClock>();
        clock.SetupGet(clock => clock.UtcNow).Returns(_now);

        return new AbstractApiPhoneNumberVerificationProvider(
            httpClientFactory.Object,
            siteService.Object,
            dataProtectionProvider,
            new DefaultPhoneNumberService(),
            clock.Object,
            NullLogger<AbstractApiPhoneNumberVerificationProvider>.Instance);
    }

    private static string Protect(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string CreateValidAbstractApiPayload()
    {
        return """
        {
            "phone": "14152007986",
            "valid": true,
            "format": {
                "international": "+14152007986",
                "local": "(415) 200-7986"
            },
            "country": {
                "code": "US",
                "name": "United States",
                "prefix": "+1"
            },
            "type": "mobile",
            "carrier": "T-Mobile USA, Inc."
        }
        """;
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public RecordingHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public HttpRequestMessage Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;

            return Task.FromResult(_response);
        }
    }

    private sealed class FakeDataProtectionProvider : IDataProtectionProvider
    {
        private readonly FakeDataProtector _protector = new();

        public IDataProtector CreateProtector(string purpose)
        {
            return _protector;
        }
    }

    private sealed class FakeDataProtector : IDataProtector
    {
        public IDataProtector CreateProtector(string purpose)
        {
            return this;
        }

        public byte[] Protect(byte[] plaintext)
        {
            return plaintext;
        }

        public byte[] Unprotect(byte[] protectedData)
        {
            return protectedData;
        }
    }
}
