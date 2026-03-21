using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Orchestration;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Playwright.BackgroundTasks;
using CrestApps.OrchardCore.AI.Playwright.Drivers;
using CrestApps.OrchardCore.AI.Playwright.Filters;
using CrestApps.OrchardCore.AI.Playwright.Handlers;
using CrestApps.OrchardCore.AI.Playwright.Services;
using CrestApps.OrchardCore.AI.Playwright.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Playwright;

[Feature(PlaywrightConstants.Feature.AdminWidget)]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IPlaywrightObservationService, PlaywrightObservationService>();
        services.AddSingleton<IPlaywrightActionVisualizer, PlaywrightActionVisualizer>();
        services.AddSingleton<IPlaywrightPageInspectionService, PlaywrightPageInspectionService>();
        services.AddSingleton<IPlaywrightSessionRequestResolver, PlaywrightSessionRequestResolver>();
        services.AddSingleton<IPlaywrightSessionManager, PlaywrightSessionManager>();
        services.AddSingleton<IOrchardAdminPlaywrightService, OrchardAdminPlaywrightService>();
        services.AddSingleton<IOrchardEvidenceService, OrchardEvidenceService>();
        services.AddSingleton<IBackgroundTask, PlaywrightSessionCleanupBackgroundTask>();

        services.AddScoped<IFeatureEventHandler, PlaywrightFeatureEventHandler>();

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IToolRegistryProvider, PlaywrightToolRegistryProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOrchestrationContextBuilderHandler, PlaywrightOrchestrationContextHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IChatInteractionSettingsHandler, PlaywrightChatInteractionSettingsHandler>());

        services.AddDisplayDriver<AIProfile, PlaywrightProfileSettingsDisplayDriver>();
        services.AddDisplayDriver<ChatInteraction, ChatInteractionPlaywrightSettingsDisplayDriver>();

        services.Configure<MvcOptions>(options =>
        {
            options.Filters.Add<PlaywrightAdminWidgetFilter>();
        });

        services.AddCoreAITool<CaptureStateTool>(PlaywrightConstants.ToolNames.CaptureState);
        services.AddCoreAITool<OpenAdminHomeTool>(PlaywrightConstants.ToolNames.OpenAdminHome);
        services.AddCoreAITool<OpenContentItemsTool>(PlaywrightConstants.ToolNames.OpenContentItems);
        services.AddCoreAITool<ListContentItemsTool>(PlaywrightConstants.ToolNames.ListContentItems);
        services.AddCoreAITool<OpenContentItemEditorTool>(PlaywrightConstants.ToolNames.OpenContentItemEditor);
        services.AddCoreAITool<OpenNewContentItemTool>(PlaywrightConstants.ToolNames.OpenNewContentItem);
        services.AddCoreAITool<OpenEditorTabTool>(PlaywrightConstants.ToolNames.OpenEditorTab);
        services.AddCoreAITool<SetContentTitleTool>(PlaywrightConstants.ToolNames.SetContentTitle);
        services.AddCoreAITool<SetFieldValueTool>(PlaywrightConstants.ToolNames.SetFieldValue);
        services.AddCoreAITool<SetBodyFieldTool>(PlaywrightConstants.ToolNames.SetBodyField);
        services.AddCoreAITool<SaveDraftTool>(PlaywrightConstants.ToolNames.SaveDraft);
        services.AddCoreAITool<PublishContentTool>(PlaywrightConstants.ToolNames.PublishContent);
        services.AddCoreAITool<PublishAndVerifyTool>(PlaywrightConstants.ToolNames.PublishAndVerify);
        services.AddCoreAITool<GetPageContentTool>(PlaywrightConstants.ToolNames.InspectPageContent);
        services.AddCoreAITool<FindElementTool>(PlaywrightConstants.ToolNames.FindElement);
        services.AddCoreAITool<CheckElementExistsTool>(PlaywrightConstants.ToolNames.CheckElementExists);
        services.AddCoreAITool<GetVisibleWidgetsTool>(PlaywrightConstants.ToolNames.GetVisibleWidgets);
        services.AddCoreAITool<TakeScreenshotTool>(PlaywrightConstants.ToolNames.TakeScreenshot);
        services.AddCoreAITool<DiagnoseOrchardActionTool>(PlaywrightConstants.ToolNames.DiagnoseOrchardAction);
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
    }
}




