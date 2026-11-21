using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Telnyx;

/// <summary>
/// DRY-7: the Telnyx API base address is a free-text site setting, and every Telnyx client sends the account's API key
/// to it as a bearer token. Nothing restricted it, so a tenant administrator could point it at the cloud metadata
/// service or an internal host. Only an https address on telnyx.com is used now, and anything else is refused at save.
/// </summary>
public sealed class TelnyxApiAddressTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("https://api.telnyx.com/v2/", true)]
    [InlineData("https://api.eu.telnyx.com/v2", true)]
    [InlineData("http://api.telnyx.com/v2/", false)]
    [InlineData("http://169.254.169.254/", false)]
    [InlineData("https://10.0.0.5/v2/", false)]
    [InlineData("https://telnyx.com.attacker.example/v2/", false)]
    [InlineData("https://user:secret@api.telnyx.com/v2/", false)]
    public void OnlyAnHttpsTelnyxAddress_IsAllowed(string url, bool allowed)
        => Assert.Equal(allowed, TelnyxApiAddress.IsAllowed(url));

    [Fact]
    public void ARefusedAddress_ResolvesToTheDefault()
    {
        Assert.Equal(TelnyxConstants.DefaultApiBaseUrl, TelnyxApiAddress.Resolve("http://169.254.169.254/"));
        Assert.Equal("https://api.eu.telnyx.com/v2/", TelnyxApiAddress.Resolve("https://api.eu.telnyx.com/v2"));
    }

    [Fact]
    public void TheTypedApiClient_NeverTakesAnAddressOutsideTelnyx()
    {
        // Arrange: a stored address from before the check, as the options read it.
        var services = new ServiceCollection();
        services.AddLogging();
        new global::CrestApps.OrchardCore.Telnyx.Startup().ConfigureServices(services);
        services.RemoveAll<IConfigureOptions<TelnyxOptions>>();
        services.RemoveAll<IOptionsChangeTokenSource<TelnyxOptions>>();
        services.Configure<TelnyxOptions>(options => options.ApiBaseUrl = "http://169.254.169.254/");

        using var provider = services.BuildServiceProvider();

        // Act
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(TelnyxApiClient));

        // Assert
        Assert.Equal(new Uri(TelnyxConstants.DefaultApiBaseUrl), client.BaseAddress);
    }
}
