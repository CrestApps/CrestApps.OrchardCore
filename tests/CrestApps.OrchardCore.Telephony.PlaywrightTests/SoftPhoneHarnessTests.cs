using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// Keeps the browser harness honest. Every other test in this project is only as good as the page it runs against:
/// when that page loaded a hand-kept copy of the widget's scripts, the soft phone grew helper files and dependencies
/// the copy never had, and the suite went on testing a phone that no longer shipped. These tests fail as soon as the
/// harness stops loading what the real widget loads.
/// </summary>
public sealed partial class SoftPhoneHarnessTests : SoftPhoneBrowserTest
{
    [Fact]
    public async Task HarnessPage_LoadsTheWidgetScriptAndEveryDependencyItDeclares_InDependencyOrder()
    {
        // Arrange
        var declared = SoftPhoneAssets.ResolveWidgetScriptResources();

        // Act
        var loaded = await ReadScriptUrlsAsync();

        // Assert - the widget's own script comes last, after everything it depends on.
        Assert.Equal(SoftPhoneAssets.WidgetScriptResource, declared[^1]);
        Assert.Equal(declared.Count - SoftPhoneAssets.OmittedDependencies.Count, loaded.Count);
        Assert.Equal("/CrestApps.OrchardCore.Telephony/scripts/soft-phone.js", loaded[^1]);
        Assert.Equal(SoftPhoneTestServer.ScriptUrls, loaded);
    }

    [Fact]
    public void EveryOmittedDependency_IsStillOneTheWidgetDeclares()
    {
        // Arrange
        var declared = SoftPhoneAssets.ResolveWidgetScriptResources();

        // Act
        var stale = SoftPhoneAssets.OmittedDependencies.Keys.Where(name => !declared.Contains(name)).ToList();

        // Assert
        Assert.True(stale.Count == 0, $"The harness omits dependencies the widget no longer declares: {string.Join(", ", stale)}. Remove them from the omission list.");
    }

    [Fact]
    public async Task EveryModuleScriptTheHarnessLoads_IsTheModulesBuiltBundle()
    {
        // Arrange
        using var client = new HttpClient { BaseAddress = new Uri(Server.BaseUrl) };
        var moduleScripts = (await ReadScriptUrlsAsync())
            .Where(url => url.StartsWith(SoftPhoneAssets.ModuleUrlPrefix, StringComparison.Ordinal))
            .ToList();

        // Act & Assert - each is served, byte for byte, from the module's wwwroot: the file the real page loads, not
        // the unbuilt source it is made from.
        Assert.NotEmpty(moduleScripts);

        foreach (var url in moduleScripts)
        {
            var served = await client.GetStringAsync(url, TestContext.Current.CancellationToken);
            var file = SoftPhoneAssets.ResolveModuleFile(url[SoftPhoneAssets.ModuleUrlPrefix.Length..]);

            Assert.Equal(await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken), served);
        }
    }

    [Theory]
    [InlineData("wwwroot/scripts/soft-phone.js")]
    [InlineData("wwwroot/scripts/telephony-client.js")]
    public async Task ServedBundle_CarriesEveryHelperItsAssetGroupIsBuiltFrom(string bundleOutput)
    {
        // Arrange
        // A helper added to Assets.json, or changed, without the bundle being rebuilt is missing from the page: the
        // phone loads, and then the first call into the helper throws.
        var exports = SoftPhoneAssets.ReadHelperExports(bundleOutput);
        var page = await OpenAsync();

        // Act
        var missing = new List<string>();

        foreach (var export in exports)
        {
            var defined = await page.EvaluateAsync<bool>(
                "([ns, name]) => !!window[ns] && typeof window[ns][name] !== 'undefined'",
                new[] { export.Namespace, export.Name });

            if (!defined)
            {
                missing.Add($"{export.Input}: {export.Namespace}.{export.Name}");
            }
        }

        // Assert
        Assert.NotEmpty(exports);
        Assert.True(missing.Count == 0, $"The served {bundleOutput} is missing helper exports; run `npx gulp build`:{Environment.NewLine}{string.Join(Environment.NewLine, missing)}");
    }

    [Fact]
    public async Task HarnessPage_BootsThePhoneWithoutAScriptError()
    {
        // Arrange
        var page = await Browser.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);

        // Act
        await page.GotoAsync(Server.BaseUrl);
        await WaitForConnectedAsync(page);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public async Task HarnessMarkup_UsesOnlyHooksTheRealWidgetViewRenders()
    {
        // Arrange
        var view = await File.ReadAllTextAsync(Path.Combine(SoftPhoneAssets.ModuleDirectory, "Views", "SoftPhoneWidget.cshtml"), TestContext.Current.CancellationToken);
        var rendered = HookPattern().Matches(view).Select(match => match.Value).ToHashSet(StringComparer.Ordinal);
        using var client = new HttpClient { BaseAddress = new Uri(Server.BaseUrl) };

        // Act
        var harness = HookPattern().Matches(await client.GetStringAsync("/", TestContext.Current.CancellationToken)).Select(match => match.Value).Distinct().ToList();
        var unknown = harness.Where(hook => !rendered.Contains(hook)).ToList();

        // Assert - a hook the real view no longer renders means the harness is driving markup that does not exist.
        Assert.NotEmpty(harness);
        Assert.True(unknown.Count == 0, $"The harness uses hooks SoftPhoneWidget.cshtml does not render: {string.Join(", ", unknown)}.");
    }

    private async Task<List<string>> ReadScriptUrlsAsync()
    {
        using var client = new HttpClient { BaseAddress = new Uri(Server.BaseUrl) };
        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

        return ScriptPattern().Matches(html).Select(match => match.Groups["src"].Value).ToList();
    }

    [GeneratedRegex("<script src=\"(?<src>[^\"]+)\"")]
    private static partial Regex ScriptPattern();

    [GeneratedRegex(@"data-telephony-[a-z-]+")]
    private static partial Regex HookPattern();
}
