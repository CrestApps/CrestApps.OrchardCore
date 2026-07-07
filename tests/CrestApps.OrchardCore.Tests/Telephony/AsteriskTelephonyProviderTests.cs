using System.Net;
using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskTelephonyProviderTests
{
    private const string PlainPassword = "secret-password";
    private const string BaseUrl = "http://asterisk.test:8088/ari/";

    [Fact]
    public async Task DialAsync_WhenConfigured_PostsToAsteriskAriWithBasicAuthentication()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"id\":\"call-1\"}");
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var result = await provider.DialAsync(new DialRequest { To = "+15551234567" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Call);
        Assert.Equal("call-1", result.Call.CallId);
        Assert.Equal(CallState.Connecting, result.Call.State);
        Assert.Equal(CallDirection.Outbound, result.Call.Direction);

        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Equal($"{BaseUrl}channels?endpoint=PJSIP%2F%2B15551234567@phones&app=crestapps-telephony&timeout=30&callerId=%2B15550000000", handler.LastRequest.RequestUri.AbsoluteUri);
        Assert.Equal("Basic", handler.LastRequest.Headers.Authorization.Scheme);
        Assert.Equal(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"ari-user:{PlainPassword}")), handler.LastRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task DialAsync_WhenDisabled_ReturnsFailedAndDoesNotCallApi()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"id\":\"call-1\"}");
        var provider = CreateProvider(handler, out _, isEnabled: false);

        // Act
        var result = await provider.DialAsync(new DialRequest { To = "+15551234567" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task GetClientCredentialsAsync_WhenConfigured_ReturnsProviderName()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var provider = CreateProvider(handler, out _, isEnabled: true);

        // Act
        var credentials = await provider.GetClientCredentialsAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(credentials);
        Assert.Equal(AsteriskConstants.ProviderTechnicalName, credentials.ProviderName);
        Assert.Null(handler.LastRequest);
    }

    private static AsteriskTelephonyProvider CreateProvider(
        StubHttpMessageHandler handler,
        out IDataProtectionProvider dataProtectionProvider,
        bool isEnabled)
    {
        dataProtectionProvider = new EphemeralDataProtectionProvider();

        var protectedPassword = dataProtectionProvider
            .CreateProtector(AsteriskConstants.ProtectorName)
            .Protect(PlainPassword);

        var settings = new AsteriskSettings
        {
            IsEnabled = isEnabled,
            BaseUrl = BaseUrl,
            UserName = "ari-user",
            Password = protectedPassword,
            ApplicationName = "crestapps-telephony",
            EndpointTemplate = "PJSIP/{number}@phones",
            OutboundCallerId = "+15550000000",
            TimeoutSeconds = 30,
        };

        return new AsteriskTelephonyProvider(
            SiteServiceFactory.Create(settings),
            dataProtectionProvider,
            new StubHttpClientFactory(handler),
            new StubClock(),
            NullLogger<AsteriskTelephonyProvider>.Instance,
            new PassThroughStringLocalizer<AsteriskTelephonyProvider>());
    }
}
