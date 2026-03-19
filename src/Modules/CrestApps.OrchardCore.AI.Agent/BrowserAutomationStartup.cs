using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.BrowserAutomation;

[Feature(AIConstants.Feature.OrchardCoreAIAgent)]
public sealed class BrowserAutomationStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public BrowserAutomationStartup(IStringLocalizer<BrowserAutomationStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<BrowserAutomationService>();

        RegisterSessionTools(services);
        RegisterNavigationTools(services);
        RegisterInspectionTools(services);
        RegisterInteractionTools(services);
        RegisterFormTools(services);
        RegisterWaitingTools(services);
        RegisterTroubleshootingTools(services);
    }

    private void RegisterSessionTools(IServiceCollection services)
    {
        services.AddAITool<StartBrowserSessionTool>(StartBrowserSessionTool.TheName)
            .WithTitle(S["Start Browser Session"])
            .WithDescription(S["Launches a Playwright browser session and optionally opens an initial page."])
            .WithCategory(S["Browser Sessions"])
            .Selectable();

        services.AddAITool<CloseBrowserSessionTool>(CloseBrowserSessionTool.TheName)
            .WithTitle(S["Close Browser Session"])
            .WithDescription(S["Closes a tracked Playwright browser session and disposes its tabs."])
            .WithCategory(S["Browser Sessions"])
            .Selectable();

        services.AddAITool<ListBrowserSessionsTool>(ListBrowserSessionsTool.TheName)
            .WithTitle(S["List Browser Sessions"])
            .WithDescription(S["Lists tracked Playwright browser sessions and their tabs."])
            .WithCategory(S["Browser Sessions"])
            .Selectable();

        services.AddAITool<GetBrowserSessionTool>(GetBrowserSessionTool.TheName)
            .WithTitle(S["Get Browser Session"])
            .WithDescription(S["Retrieves details about a tracked Playwright browser session."])
            .WithCategory(S["Browser Sessions"])
            .Selectable();

        services.AddAITool<OpenBrowserTabTool>(OpenBrowserTabTool.TheName)
            .WithTitle(S["Open Browser Tab"])
            .WithDescription(S["Opens a new tab in a tracked browser session."])
            .WithCategory(S["Browser Sessions"])
            .Selectable();

        services.AddAITool<CloseBrowserTabTool>(CloseBrowserTabTool.TheName)
            .WithTitle(S["Close Browser Tab"])
            .WithDescription(S["Closes a tab in a tracked browser session."])
            .WithCategory(S["Browser Sessions"])
            .Selectable();

        services.AddAITool<SwitchBrowserTabTool>(SwitchBrowserTabTool.TheName)
            .WithTitle(S["Switch Browser Tab"])
            .WithDescription(S["Marks a browser tab as the active tab for subsequent actions."])
            .WithCategory(S["Browser Sessions"])
            .Selectable();
    }

    private void RegisterNavigationTools(IServiceCollection services)
    {
        services.AddAITool<NavigateBrowserTool>(NavigateBrowserTool.TheName)
            .WithTitle(S["Navigate Browser"])
            .WithDescription(S["Navigates a tab to a URL."])
            .WithCategory(S["Browser Navigation"])
            .Selectable();

        services.AddAITool<GoBackBrowserTool>(GoBackBrowserTool.TheName)
            .WithTitle(S["Go Back"])
            .WithDescription(S["Navigates backward in browser history."])
            .WithCategory(S["Browser Navigation"])
            .Selectable();

        services.AddAITool<GoForwardBrowserTool>(GoForwardBrowserTool.TheName)
            .WithTitle(S["Go Forward"])
            .WithDescription(S["Navigates forward in browser history."])
            .WithCategory(S["Browser Navigation"])
            .Selectable();

        services.AddAITool<ReloadBrowserPageTool>(ReloadBrowserPageTool.TheName)
            .WithTitle(S["Reload Page"])
            .WithDescription(S["Reloads the current page."])
            .WithCategory(S["Browser Navigation"])
            .Selectable();

        services.AddAITool<ScrollBrowserPageTool>(ScrollBrowserPageTool.TheName)
            .WithTitle(S["Scroll Page"])
            .WithDescription(S["Scrolls the current page vertically or horizontally."])
            .WithCategory(S["Browser Navigation"])
            .Selectable();

        services.AddAITool<ScrollBrowserElementIntoViewTool>(ScrollBrowserElementIntoViewTool.TheName)
            .WithTitle(S["Scroll Element Into View"])
            .WithDescription(S["Scrolls a specific element into the viewport."])
            .WithCategory(S["Browser Navigation"])
            .Selectable();
    }

    private void RegisterInspectionTools(IServiceCollection services)
    {
        services.AddAITool<GetBrowserPageStateTool>(GetBrowserPageStateTool.TheName)
            .WithTitle(S["Get Page State"])
            .WithDescription(S["Returns high-level state for the current page."])
            .WithCategory(S["Browser Inspection"])
            .Selectable();

        services.AddAITool<GetBrowserPageContentTool>(GetBrowserPageContentTool.TheName)
            .WithTitle(S["Get Page Content"])
            .WithDescription(S["Returns page text and HTML for the full page or a selected element."])
            .WithCategory(S["Browser Inspection"])
            .Selectable();

        services.AddAITool<GetBrowserLinksTool>(GetBrowserLinksTool.TheName)
            .WithTitle(S["Get Page Links"])
            .WithDescription(S["Lists the links found on the current page."])
            .WithCategory(S["Browser Inspection"])
            .Selectable();

        services.AddAITool<GetBrowserFormsTool>(GetBrowserFormsTool.TheName)
            .WithTitle(S["Get Page Forms"])
            .WithDescription(S["Lists forms and their fields on the current page."])
            .WithCategory(S["Browser Inspection"])
            .Selectable();

        services.AddAITool<GetBrowserHeadingsTool>(GetBrowserHeadingsTool.TheName)
            .WithTitle(S["Get Page Headings"])
            .WithDescription(S["Lists headings found on the current page."])
            .WithCategory(S["Browser Inspection"])
            .Selectable();

        services.AddAITool<GetBrowserButtonsTool>(GetBrowserButtonsTool.TheName)
            .WithTitle(S["Get Page Buttons"])
            .WithDescription(S["Lists button-like controls found on the current page."])
            .WithCategory(S["Browser Inspection"])
            .Selectable();

        services.AddAITool<GetBrowserElementInfoTool>(GetBrowserElementInfoTool.TheName)
            .WithTitle(S["Get Element Info"])
            .WithDescription(S["Returns detailed information about a selected element."])
            .WithCategory(S["Browser Inspection"])
            .Selectable();
    }

    private void RegisterInteractionTools(IServiceCollection services)
    {
        services.AddAITool<ClickBrowserElementTool>(ClickBrowserElementTool.TheName)
            .WithTitle(S["Click Element"])
            .WithDescription(S["Clicks a page element."])
            .WithCategory(S["Browser Interaction"])
            .Selectable();

        services.AddAITool<DoubleClickBrowserElementTool>(DoubleClickBrowserElementTool.TheName)
            .WithTitle(S["Double Click Element"])
            .WithDescription(S["Double-clicks a page element."])
            .WithCategory(S["Browser Interaction"])
            .Selectable();

        services.AddAITool<HoverBrowserElementTool>(HoverBrowserElementTool.TheName)
            .WithTitle(S["Hover Element"])
            .WithDescription(S["Moves the mouse over a page element."])
            .WithCategory(S["Browser Interaction"])
            .Selectable();

        services.AddAITool<PressBrowserKeyTool>(PressBrowserKeyTool.TheName)
            .WithTitle(S["Press Key"])
            .WithDescription(S["Sends a keyboard key or shortcut to the page."])
            .WithCategory(S["Browser Interaction"])
            .Selectable();
    }

    private void RegisterFormTools(IServiceCollection services)
    {
        services.AddAITool<FillBrowserInputTool>(FillBrowserInputTool.TheName)
            .WithTitle(S["Fill Input"])
            .WithDescription(S["Fills an input, textarea, or editable element."])
            .WithCategory(S["Browser Forms"])
            .Selectable();

        services.AddAITool<ClearBrowserInputTool>(ClearBrowserInputTool.TheName)
            .WithTitle(S["Clear Input"])
            .WithDescription(S["Clears an input or textarea value."])
            .WithCategory(S["Browser Forms"])
            .Selectable();

        services.AddAITool<SelectBrowserOptionTool>(SelectBrowserOptionTool.TheName)
            .WithTitle(S["Select Option"])
            .WithDescription(S["Selects one or more values in a select element."])
            .WithCategory(S["Browser Forms"])
            .Selectable();

        services.AddAITool<CheckBrowserElementTool>(CheckBrowserElementTool.TheName)
            .WithTitle(S["Check Element"])
            .WithDescription(S["Checks a checkbox or radio button."])
            .WithCategory(S["Browser Forms"])
            .Selectable();

        services.AddAITool<UncheckBrowserElementTool>(UncheckBrowserElementTool.TheName)
            .WithTitle(S["Uncheck Element"])
            .WithDescription(S["Unchecks a checkbox."])
            .WithCategory(S["Browser Forms"])
            .Selectable();

        services.AddAITool<UploadBrowserFilesTool>(UploadBrowserFilesTool.TheName)
            .WithTitle(S["Upload Files"])
            .WithDescription(S["Uploads local files into a file input element."])
            .WithCategory(S["Browser Forms"])
            .Selectable();
    }

    private void RegisterWaitingTools(IServiceCollection services)
    {
        services.AddAITool<WaitForBrowserElementTool>(WaitForBrowserElementTool.TheName)
            .WithTitle(S["Wait For Element"])
            .WithDescription(S["Waits for a selector to reach a requested state."])
            .WithCategory(S["Browser Waiting"])
            .Selectable();

        services.AddAITool<WaitForBrowserNavigationTool>(WaitForBrowserNavigationTool.TheName)
            .WithTitle(S["Wait For Navigation"])
            .WithDescription(S["Waits for navigation or a URL change."])
            .WithCategory(S["Browser Waiting"])
            .Selectable();

        services.AddAITool<WaitForBrowserLoadStateTool>(WaitForBrowserLoadStateTool.TheName)
            .WithTitle(S["Wait For Load State"])
            .WithDescription(S["Waits for a page to reach a specific load state."])
            .WithCategory(S["Browser Waiting"])
            .Selectable();
    }

    private void RegisterTroubleshootingTools(IServiceCollection services)
    {
        services.AddAITool<CaptureBrowserScreenshotTool>(CaptureBrowserScreenshotTool.TheName)
            .WithTitle(S["Capture Screenshot"])
            .WithDescription(S["Captures a screenshot of the current page."])
            .WithCategory(S["Browser Troubleshooting"])
            .Selectable();

        services.AddAITool<GetBrowserConsoleMessagesTool>(GetBrowserConsoleMessagesTool.TheName)
            .WithTitle(S["Get Console Messages"])
            .WithDescription(S["Returns recent console messages and page errors."])
            .WithCategory(S["Browser Troubleshooting"])
            .Selectable();

        services.AddAITool<GetBrowserNetworkActivityTool>(GetBrowserNetworkActivityTool.TheName)
            .WithTitle(S["Get Network Activity"])
            .WithDescription(S["Returns recent network requests and responses."])
            .WithCategory(S["Browser Troubleshooting"])
            .Selectable();

        services.AddAITool<DiagnoseBrowserPageTool>(DiagnoseBrowserPageTool.TheName)
            .WithTitle(S["Diagnose Page"])
            .WithDescription(S["Collects a troubleshooting snapshot for the current page."])
            .WithCategory(S["Browser Troubleshooting"])
            .Selectable();
    }
}

