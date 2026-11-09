using System.Net;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Commerce.Stores;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Commerce.IntegrationTests;

/// <summary>
/// Settings › Commerce › Store renders for an administrator, with the address editor, and saves the store profile.
/// </summary>
[Collection(CommerceApiCollection.Name)]
public sealed partial class StoreProfilePageTests
{
    private readonly CommerceApiFixture _fixture;

    public StoreProfilePageTests(CommerceApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TheStorePage_Renders_AndSavesTheProfile()
    {
        using var http = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false })
        {
            BaseAddress = _fixture.BaseAddress,
        };

        var login = await http.GetStringAsync("Login", TestContext.Current.CancellationToken);
        var signedIn = await http.PostAsync("Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["LoginForm.UserName"] = "admin",
            ["LoginForm.Password"] = _fixture.Password,
            ["__RequestVerificationToken"] = Token(login),
        }), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);

        var page = await http.GetAsync("Admin/Settings/store-profile", TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("name=\"ISite.StoreProfileSettings.Name\"", html);
        Assert.Contains("name=\"ISite.StoreProfileSettings.Address.AddressLine1\"", html);
        Assert.Contains("card-header", html);

        var saved = await http.PostAsync("Admin/Settings/store-profile", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(html),
            ["GroupId"] = "store-profile",
            ["ISite.StoreProfileSettings.Name"] = "Contoso Goods",
            ["ISite.StoreProfileSettings.TaxId"] = "EIN-1",
            ["ISite.StoreProfileSettings.Email"] = "hello@contoso.test",
            ["ISite.StoreProfileSettings.Phone"] = "(206) 555-0100",
            ["ISite.StoreProfileSettings.Address.Country"] = "US",
            ["ISite.StoreProfileSettings.Address.AddressLine1"] = "500 Pine St",
            ["ISite.StoreProfileSettings.Address.City"] = "Seattle",
            ["ISite.StoreProfileSettings.Address.Region"] = "WA",
            ["ISite.StoreProfileSettings.Address.PostalCode"] = "98101",
        }), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

        var profile = await _fixture.InTenantAsync(services => services.GetRequiredService<ISiteService>().GetStoreProfileAsync());

        Assert.Equal("Contoso Goods", profile.Name);
        Assert.Equal("EIN-1", profile.TaxId);
        Assert.Equal("Seattle", profile.Address?.City);
        Assert.Equal("WA", profile.Address?.Region);
        Assert.Contains("555", profile.Phone);
    }

    private static string Token(string html)
        => TokenPattern().Match(html) is { Success: true } match ? WebUtility.HtmlDecode(match.Groups["token"].Value) : throw new InvalidOperationException("The page has no antiforgery token.");

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"")]
    private static partial Regex TokenPattern();
}
