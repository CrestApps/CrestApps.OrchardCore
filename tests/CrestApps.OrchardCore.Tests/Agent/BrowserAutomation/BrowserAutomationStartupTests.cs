using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Agent.BrowserAutomation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Agent.BrowserAutomation;

public sealed class BrowserAutomationStartupTests
{
    [Fact]
    public void ConfigureServices_RegistersExpectedSelectableBrowserTools()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddLogging();

        var startup = new BrowserAutomationStartup(new PassthroughStringLocalizer<BrowserAutomationStartup>());

        startup.ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();
        var definitions = serviceProvider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value;
        var selectableTools = definitions.Tools
            .Where(x => !x.Value.IsSystemTool)
            .ToDictionary(x => x.Key, x => x.Value);

        Assert.Equal(37, selectableTools.Count);

        Assert.Equal(7, selectableTools.Count(x => x.Value.Category == "Browser Sessions"));
        Assert.Equal(6, selectableTools.Count(x => x.Value.Category == "Browser Navigation"));
        Assert.Equal(7, selectableTools.Count(x => x.Value.Category == "Browser Inspection"));
        Assert.Equal(4, selectableTools.Count(x => x.Value.Category == "Browser Interaction"));
        Assert.Equal(6, selectableTools.Count(x => x.Value.Category == "Browser Forms"));
        Assert.Equal(3, selectableTools.Count(x => x.Value.Category == "Browser Waiting"));
        Assert.Equal(4, selectableTools.Count(x => x.Value.Category == "Browser Troubleshooting"));

        Assert.Contains(StartBrowserSessionTool.TheName, selectableTools.Keys);
        Assert.Contains(NavigateBrowserTool.TheName, selectableTools.Keys);
        Assert.Contains(WaitForBrowserElementTool.TheName, selectableTools.Keys);
        Assert.Contains(DiagnoseBrowserPageTool.TheName, selectableTools.Keys);
    }

    private sealed class PassthroughStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
