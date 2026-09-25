using System.Net;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Tests.Telnyx;

/// <summary>
/// Telnyx refuses to message a number that texted STOP, and says so with its own code. The provider maps that
/// code onto the provider-neutral reason, so the sender records the opt-out instead of retrying.
/// </summary>
public sealed class TelnyxSmsProviderOptOutTests
{
    [Fact]
    public async Task DispatchAsync_WhenTelnyxBlocksAStoppedRecipient_ReportsTheOptOut()
    {
        // Arrange
        const string Body = """{"errors":[{"code":"40300","title":"Blocked due to STOP message","detail":"Messages cannot be sent to this recipient."}]}""";
        var provider = CreateProvider(new StubHttpMessageHandler(HttpStatusCode.BadRequest, Body));

        // Act
        var result = await provider.DispatchAsync(Message(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(OmnichannelConstants.SmsErrorCodes.RecipientOptedOut, result.ErrorCode);
    }

    [Fact]
    public async Task DispatchAsync_WhenTelnyxRefusesForAnotherReason_ReportsNoReason()
    {
        // Arrange
        const string Body = """{"errors":[{"code":"40310","title":"Invalid 'to' address"}]}""";
        var provider = CreateProvider(new StubHttpMessageHandler(HttpStatusCode.BadRequest, Body));

        // Act
        var result = await provider.DispatchAsync(Message(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(result.ErrorCode);
    }

    private static SmsMessage Message()
        => new() { From = "+15553334444", To = "+15551112222", Body = "hi" };

    private static TelnyxSmsProvider CreateProvider(HttpMessageHandler handler)
        => new(
            new StubHttpClientFactory(handler),
            new TestOptionsMonitor<TelnyxSmsOptions>(new TelnyxSmsOptions { IsEnabled = true, ApiKey = "key" }),
            NullLogger<TelnyxSmsProvider>.Instance,
            new PassThroughStringLocalizer<TelnyxSmsProvider>());
}
