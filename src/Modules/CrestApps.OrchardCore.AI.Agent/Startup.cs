using CrestApps.OrchardCore.AI.Agent.Services;
using CrestApps.OrchardCore.AI.Agent.Tools.BrowserAutomation;
using CrestApps.OrchardCore.AI.Agent.Tools.Analytics;
using CrestApps.OrchardCore.AI.Agent.Tools.Communications;
using CrestApps.OrchardCore.AI.Agent.Tools.Contents;
using CrestApps.OrchardCore.AI.Agent.Tools.ContentTypes;
using CrestApps.OrchardCore.AI.Agent.Tools.Features;
using CrestApps.OrchardCore.AI.Agent.Tools.Profiles;
using CrestApps.OrchardCore.AI.Agent.Tools.Recipes;
using CrestApps.OrchardCore.AI.Agent.Tools.Roles;
using CrestApps.OrchardCore.AI.Agent.Tools.System;
using CrestApps.OrchardCore.AI.Agent.Tools.Tenants;
using CrestApps.OrchardCore.AI.Agent.Tools.Users;
using CrestApps.OrchardCore.AI.Agent.Tools.Workflows;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

[Feature(AIConstants.Feature.OrchardCoreAIAgent)]
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<ListTimeZoneTool>(ListTimeZoneTool.TheName)
            .WithTitle(S["List System Time Zones"])
            .WithDescription(S["Retrieves a list of the available time zones in the system."])
            .WithCategory(S["System"])
            .Selectable();
    }
}

[Feature(AIConstants.Feature.OrchardCoreAIAgentBrowserAutomation)]
public sealed class BrowserAutomationFeatureStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public BrowserAutomationFeatureStartup(IStringLocalizer<BrowserAutomationFeatureStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<BrowserAutomationService>();

        RegisterBrowserSessionTools(services);
        RegisterBrowserNavigationTools(services);
        RegisterBrowserInspectionTools(services);
        RegisterBrowserInteractionTools(services);
        RegisterBrowserFormTools(services);
        RegisterBrowserWaitingTools(services);
        RegisterBrowserTroubleshootingTools(services);
    }

    private void RegisterBrowserSessionTools(IServiceCollection services)
    {
        services.AddAITool<StartBrowserSessionTool>(StartBrowserSessionTool.TheName)
            .WithTitle(S["Start Browser Session"])
            .WithDescription(S["Launches a real Playwright browser session so the AI can visit pages, inspect navigation, and interact with the website UI. Start with this before using the other browser tools."])
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

    private void RegisterBrowserNavigationTools(IServiceCollection services)
    {
        services.AddAITool<NavigateBrowserTool>(NavigateBrowserTool.TheName)
            .WithTitle(S["Navigate Browser"])
            .WithDescription(S["Visits a specific URL in the real browser. Use this when the destination URL is known."])
            .WithCategory(S["Browser Navigation"])
            .Selectable();

        services.AddAITool<NavigateBrowserMenuTool>(NavigateBrowserMenuTool.TheName)
            .WithTitle(S["Navigate Menu"])
            .WithDescription(S["Opens a page from visible site navigation or Orchard Core admin sidebar labels, including nested paths like 'Search >> Indexes' or 'Content Management >> Content Definitions'."])
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

    private void RegisterBrowserInspectionTools(IServiceCollection services)
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
            .WithDescription(S["Lists visible links and navigation items found on the current page, including sidebar or menu entries."])
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

    private void RegisterBrowserInteractionTools(IServiceCollection services)
    {
        services.AddAITool<ClickBrowserElementTool>(ClickBrowserElementTool.TheName)
            .WithTitle(S["Click Element"])
            .WithDescription(S["Clicks a visible page element, such as a link, button, or menu item."])
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

    private void RegisterBrowserFormTools(IServiceCollection services)
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

    private void RegisterBrowserWaitingTools(IServiceCollection services)
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

    private void RegisterBrowserTroubleshootingTools(IServiceCollection services)
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

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public RecipesStartup(IStringLocalizer<RecipesStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<ApplySystemSettingsTool>(ApplySystemSettingsTool.TheName)
            .WithTitle(S["Apply Site Configuration"])
            .WithDescription(S["Applies predefined system configurations and settings using AI assistance."])
            .WithCategory(S["Recipes"])
            .Selectable();

        services.AddAITool<GetRecipeJsonSchemaTool>(GetRecipeJsonSchemaTool.TheName)
            .WithTitle(S["Get Orchard Core Recipe JSON Schema"])
            .WithDescription(S["Returns a JSON Schema definition for Orchard Core recipes or a specific recipe step."])
            .WithCategory(S["Recipes"])
            .Selectable();

        services.AddAITool<ListRecipeStepsAndSchemasTool>(ListRecipeStepsAndSchemasTool.TheName)
            .WithTitle(S["List Orchard Core Recipe Steps and Schemas"])
            .WithDescription(S["Lists all available Orchard Core recipe steps and returns their JSON schema definitions."])
            .WithCategory(S["Recipes"])
            .Selectable();

        services.AddAITool<ImportOrchardTool>(ImportOrchardTool.TheName)
            .WithTitle(S["Import Orchard Core Recipe"])
            .WithDescription(S["Enables AI agents to import and run Orchard Core recipes within your site."])
            .WithCategory(S["Recipes"])
            .Selectable();

        services.AddAITool<ListNonStartupRecipesTool>(ListNonStartupRecipesTool.TheName)
            .WithTitle(S["List Non-Startup Recipes"])
            .WithDescription(S["Retrieves all available Orchard Core recipes that are not executed during startup."])
            .WithCategory(S["Recipes"])
            .Selectable();

        services.AddAITool<ExecuteStartupRecipesTool>(ExecuteStartupRecipesTool.TheName)
            .WithTitle(S["Run Non-Startup Recipes"])
            .WithDescription(S["Executes Orchard Core recipes that are not configured to run at application startup."])
            .WithCategory(S["Recipes"])
            .Selectable();
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Tenants")]
public sealed class TenantsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public TenantsStartup(IStringLocalizer<TenantsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<ListStartupRecipesTool>(ListStartupRecipesTool.TheName)
            .WithTitle(S["List Startup Recipes"])
            .WithDescription(S["Retrieves a list of Orchard Core recipes configured to run at application startup."])
            .WithCategory(S["Tenants Management"])
            .Selectable();

        services.AddAITool<CreateTenantTool>(CreateTenantTool.TheName)
            .WithTitle(S["Create Tenant"])
            .WithDescription(S["Creates a new tenant in the Orchard Core application."])
            .WithCategory(S["Tenants Management"])
            .Selectable();

        services.AddAITool<GetTenantTool>(GetTenantTool.TheName)
            .WithTitle(S["Get Tenant Information"])
            .WithDescription(S["Retrieves detailed information about a specific tenant."])
            .WithCategory(S["Tenants Management"])
            .Selectable();

        services.AddAITool<ListTenantTool>(ListTenantTool.TheName)
            .WithTitle(S["List All Tenants"])
            .WithDescription(S["Returns information about all tenants in the system."])
            .WithCategory(S["Tenants Management"])
            .Selectable();

        services.AddAITool<EnableTenantTool>(EnableTenantTool.TheName)
            .WithTitle(S["Enable Tenant"])
            .WithDescription(S["Enables a tenant that is currently disabled."])
            .WithCategory(S["Tenants Management"])
            .Selectable();

        services.AddAITool<DisableTenantTool>(DisableTenantTool.TheName)
            .WithTitle(S["Disable Tenant"])
            .WithDescription(S["Disables a tenant that is currently active."])
            .WithCategory(S["Tenants Management"])
            .Selectable();

        services.AddAITool<RemoveTenantTool>(RemoveTenantTool.TheName)
            .WithTitle(S["Remove Tenant"])
            .WithDescription(S["Removes an existing tenant that can be safely deleted."])
            .WithCategory(S["Tenants Management"])
            .Selectable();

        services.AddAITool<ReloadTenantTool>(ReloadTenantTool.TheName)
            .WithTitle(S["Reload Tenant"])
            .WithDescription(S["Reloads the configuration and state of an existing tenant."])
            .WithCategory(S["Tenants Management"])
            .Selectable();

        services.AddAITool<SetupTenantTool>(SetupTenantTool.TheName)
            .WithTitle(S["Setup Tenant"])
            .WithDescription(S["Sets up new tenants."])
            .WithCategory(S["Tenants Management"])
            .Selectable();
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Contents")]
public sealed class ContentsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public ContentsStartup(IStringLocalizer<ContentsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<SearchForContentsTool>(SearchForContentsTool.TheName)
            .WithTitle(S["Search Content Items"])
            .WithDescription(S["Provides a way to search for content items."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddAITool<GetContentItemSchemaTool>(GetContentItemSchemaTool.TheName)
            .WithTitle(S["Generate Content Item Sample"])
            .WithDescription(S["Generates a structured sample content item for a specified content type."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddAITool<PublishContentTool>(PublishContentTool.TheName)
            .WithTitle(S["Publish Content Item"])
            .WithDescription(S["Publishes a draft or previously unpublished content item."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddAITool<UnpublishContentTool>(UnpublishContentTool.TheName)
            .WithTitle(S["Unpublish Content Item"])
            .WithDescription(S["Unpublishes a currently published content item."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddAITool<GetContentTool>(GetContentTool.TheName)
            .WithTitle(S["Retrieve Content Item"])
            .WithDescription(S["Retrieves a specific content item by its ID or type."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddAITool<DeleteContentTool>(DeleteContentTool.TheName)
            .WithTitle(S["Delete Content Item"])
            .WithDescription(S["Deletes a content item from the system."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddAITool<CloneContentTool>(CloneContentTool.TheName)
            .WithTitle(S["Clone Content Item"])
            .WithDescription(S["Creates a duplicate of an existing content item."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddAITool<CreateOrUpdateContentTool>(CreateOrUpdateContentTool.TheName)
            .WithTitle(S["Create or Update Content Item"])
            .WithDescription(S["Creates a new content item or updates an existing one."])
            .WithCategory(S["Content Management"])
            .Selectable();

        services.AddAITool<GetContentItemLinkTool>(GetContentItemLinkTool.TheName)
            .WithTitle(S["Retrieve a Link for a Content Item"])
            .WithDescription(S["Retrieves a link for a content item."])
            .WithCategory(S["Content Management"])
            .Selectable();
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.ContentTypes")]
public sealed class ContentDefinitionsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public ContentDefinitionsStartup(IStringLocalizer<ContentDefinitionsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ContentMetadataService>();

        services.AddAITool<GetContentTypeDefinitionsTool>(GetContentTypeDefinitionsTool.TheName)
            .WithTitle(S["Get Content Type Definitions"])
            .WithDescription(S["Retrieves the definitions of all available content types."])
            .WithCategory(S["Content Definitions"])
            .Selectable();

        services.AddAITool<GetContentPartDefinitionsTool>(GetContentPartDefinitionsTool.TheName)
            .WithTitle(S["Get Content Part Definitions"])
            .WithDescription(S["Retrieves the definitions of all available content parts."])
            .WithCategory(S["Content Definitions"])
            .Selectable();

        services.AddAITool<ListContentTypesDefinitionsTool>(ListContentTypesDefinitionsTool.TheName)
            .WithTitle(S["List Available Content Types Definitions"])
            .WithDescription(S["Provides a list of available content types definitions."])
            .WithCategory(S["Content Definitions"])
            .Selectable();

        services.AddAITool<ListContentPartsDefinitionsTool>(ListContentPartsDefinitionsTool.TheName)
            .WithTitle(S["List Available Content Parts Definitions"])
            .WithDescription(S["Provides a list of available content parts definitions."])
            .WithCategory(S["Content Definitions"])
            .Selectable();

        services.AddAITool<ListContentFieldsTool>(ListContentFieldsTool.TheName)
            .WithTitle(S["List Available Content Fields"])
            .WithDescription(S["Provides a list of available content fields."])
            .WithCategory(S["Content Definitions"])
            .Selectable();
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.ContentTypes", "OrchardCore.Recipes.Core")]
public sealed class ContentDefinitionRecipesToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public ContentDefinitionRecipesToolsStartup(IStringLocalizer<ContentDefinitionRecipesToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<RemoveContentTypeDefinitionsTool>(RemoveContentTypeDefinitionsTool.TheName)
            .WithTitle(S["Remove Content Type Definitions"])
            .WithDescription(S["Removes the content type definition."])
            .WithCategory(S["Content Definitions"])
            .Selectable();

        services.AddAITool<RemoveContentPartDefinitionsTool>(RemoveContentPartDefinitionsTool.TheName)
            .WithTitle(S["Remove Content Part Definitions"])
            .WithDescription(S["Removes the content part definition."])
            .WithCategory(S["Content Definitions"])
            .Selectable();

        services.AddAITool<CreateOrUpdateContentTypeDefinitionsTool>(CreateOrUpdateContentTypeDefinitionsTool.TheName)
            .WithTitle(S["Create or Update Content Type Definition"])
            .WithDescription(S["Creates a new content type definition or updates an existing one."])
            .WithCategory(S["Content Definitions"])
            .Selectable();
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Features")]
public sealed class FeaturesStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public FeaturesStartup(IStringLocalizer<FeaturesStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<DisableFeatureTool>(DisableFeatureTool.TheName)
            .WithTitle(S["Disable Site Feature"])
            .WithDescription(S["Disabled site features."])
            .WithCategory(S["Features Management"])
            .Selectable();

        services.AddAITool<EnableFeatureTool>(EnableFeatureTool.TheName)
            .WithTitle(S["Enable Site Feature"])
            .WithDescription(S["Enable site features."])
            .WithCategory(S["Features Management"])
            .Selectable();

        services.AddAITool<FeaturesSearchTool>(FeaturesSearchTool.TheName)
            .WithTitle(S["Search for Site Feature"])
            .WithDescription(S["Search available features for a match."])
            .WithCategory(S["Features Management"])
            .Selectable();

        services.AddAITool<ListFeaturesTool>(ListFeaturesTool.TheName)
            .WithTitle(S["List Site Features"])
            .WithDescription(S["Retrieves available site features."])
            .WithCategory(S["Features Management"])
            .Selectable();

        services.AddAITool<GetFeatureTool>(GetFeatureTool.TheName)
            .WithTitle(S["Get Site Features"])
            .WithDescription(S["Retrieves info about a feature."])
            .WithCategory(S["Features Management"])
            .Selectable();
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Notifications")]
public sealed class NotificationsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public NotificationsStartup(IStringLocalizer<NotificationsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<SendNotificationTool>(SendNotificationTool.TheName)
            .WithTitle(S["Send User Notification"])
            .WithDescription(S["Sends a notification message to a user."])
            .WithCategory(S["Communications"])
            .Selectable();
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Email")]
public sealed class EmailStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public EmailStartup(IStringLocalizer<EmailStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<SendEmailTool>(SendEmailTool.TheName)
            .WithTitle(S["Send Emails"])
            .WithDescription(S["Sends a email message on the behalf of the logged user."])
            .WithCategory(S["Communications"])
            .Selectable();
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Sms")]
public sealed class SmsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public SmsStartup(IStringLocalizer<SmsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<SendSmsTool>(SendSmsTool.TheName)
            .WithTitle(S["Send SMS message"])
            .WithDescription(S["Sends a SMS message to a user."])
            .WithCategory(S["Communications"])
            .Selectable();
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Users")]
public sealed class UsersStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public UsersStartup(IStringLocalizer<UsersStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<GetUserInfoTool>(GetUserInfoTool.TheName)
            .WithTitle(S["Get User Info"])
            .WithDescription(S["Gets information about a user."])
            .WithCategory(S["Users Management"])
            .Selectable();

        services.AddAITool<SearchForUsersTool>(SearchForUsersTool.TheName)
            .WithTitle(S["Search Users"])
            .WithDescription(S["Search the system for users."])
            .WithCategory(S["Users Management"])
            .Selectable();
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Roles")]
public sealed class RolesStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public RolesStartup(IStringLocalizer<RolesStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<GetRoleTool>(GetRoleTool.TheName)
            .WithTitle(S["Get Role Info"])
            .WithDescription(S["Gets information about a role."])
            .WithCategory(S["Roles Management"])
            .Selectable();
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Workflows")]
public sealed class WorkflowsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public WorkflowsStartup(IStringLocalizer<WorkflowsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<GetWorkflowTypesTool>(GetWorkflowTypesTool.TheName)
            .WithTitle(S["Get Workflow Type Info"])
            .WithDescription(S["Gets information about a workflow type."])
            .WithCategory(S["Workflow Management"])
            .Selectable();

        services.AddAITool<ListWorkflowTypesTool>(ListWorkflowTypesTool.TheName)
            .WithTitle(S["List Workflow Type"])
            .WithDescription(S["List information about a workflow types."])
            .WithCategory(S["Workflow Management"])
            .Selectable();
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, "OrchardCore.Workflows", "OrchardCore.Recipes.Core")]
public sealed class WorkflowsRecipesStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public WorkflowsRecipesStartup(IStringLocalizer<WorkflowsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<CreateOrUpdateWorkflowTool>(CreateOrUpdateWorkflowTool.TheName)
            .WithTitle(S["Create Workflows"])
            .WithDescription(S["Create or update information a workflow."])
            .WithCategory(S["Workflow Management"])
            .Selectable();

        services.AddAITool<ListWorkflowActivitiesTool>(ListWorkflowActivitiesTool.TheName)
            .WithTitle(S["List Workflow Activities"])
            .WithDescription(S["List all available tasks and activities a workflow."])
            .WithCategory(S["Workflow Management"])
            .Selectable();
    }
}

[Feature(AIConstants.Feature.OrchardCoreAIAgent)]
public sealed class ProfilesStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public ProfilesStartup(IStringLocalizer<ProfilesStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<ListAIProfilesTool>(ListAIProfilesTool.TheName)
            .WithTitle(S["List AI Profiles"])
            .WithDescription(S["Lists AI profiles with optional filters for type, analytics, data extraction, and post-session processing."])
            .WithCategory(S["AI Profiles"])
            .Selectable();

        services.AddAITool<ViewAIProfileTool>(ViewAIProfileTool.TheName)
            .WithTitle(S["View AI Profile"])
            .WithDescription(S["Retrieves detailed configuration for a specific AI profile by ID or name."])
            .WithCategory(S["AI Profiles"])
            .Selectable();
    }
}

[RequireFeatures(AIConstants.Feature.OrchardCoreAIAgent, AIConstants.Feature.ChatAnalytics)]
public sealed class ChatAnalyticsToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public ChatAnalyticsToolsStartup(IStringLocalizer<ChatAnalyticsToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAITool<QueryChatSessionMetricsTool>(QueryChatSessionMetricsTool.TheName)
            .WithTitle(S["Query Chat Session Metrics"])
            .WithDescription(S["Queries aggregated chat session analytics metrics with optional date range and profile filters. Returns statistics for generating charts and reports."])
            .WithCategory(S["AI Analytics"])
            .Selectable();
    }
}
