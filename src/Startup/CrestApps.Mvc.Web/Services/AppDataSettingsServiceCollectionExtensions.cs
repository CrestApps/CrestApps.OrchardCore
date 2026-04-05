using CrestApps.AI.A2A.Models;
using CrestApps.AI.Mcp.Models;
using CrestApps.AI.Models;
using CrestApps.Mvc.Web.Areas.Admin.Models;
using CrestApps.Mvc.Web.Areas.AIChat.Models;
using CrestApps.Mvc.Web.Areas.ChatInteractions.Models;
using CrestApps.Mvc.Web.Models;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.Mvc.Web.Services;

public static class AppDataSettingsServiceCollectionExtensions
{
    public static IServiceCollection AddAppDataSettings<T>(this IServiceCollection services, IConfiguration configuration, string sectionKey)
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionKey);

        services.Configure<T>(configuration.GetSection(sectionKey));
        services.TryAddSingleton<AppDataSettingsSectionResolver>();
        services.AddSingleton(new AppDataSettingsRegistration(typeof(T), sectionKey));
        services.AddSingleton<AppDataSettingsService<T>>();

        return services;
    }

    public static IServiceCollection AddMvcAppDataSettings(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<GeneralAIOptions>(configuration.GetSection(AppDataConfigurationSections.GeneralAISettings));
        services.Configure<AIMemoryOptions>(configuration.GetSection(AppDataConfigurationSections.AIMemory));
        services.Configure<InteractionDocumentOptions>(configuration.GetSection(AppDataConfigurationSections.InteractionDocuments));
        services.Configure<AIDataSourceOptions>(configuration.GetSection(AppDataConfigurationSections.AIDataSources));
        services.Configure<ChatInteractionMemoryOptions>(configuration.GetSection(AppDataConfigurationSections.ChatInteractionMemory));

        return services
            .AddAppDataSettings<GeneralAISettings>(configuration, AppDataConfigurationSections.GeneralAISettings)
            .AddAppDataSettings<DefaultOrchestratorSettings>(configuration, AppDataConfigurationSections.DefaultOrchestrator)
            .AddAppDataSettings<DefaultAIDeploymentSettings>(configuration, AppDataConfigurationSections.DefaultDeployments)
            .AddAppDataSettings<AIMemorySettings>(configuration, AppDataConfigurationSections.AIMemory)
            .AddAppDataSettings<InteractionDocumentSettings>(configuration, AppDataConfigurationSections.InteractionDocuments)
            .AddAppDataSettings<AIDataSourceSettings>(configuration, AppDataConfigurationSections.AIDataSources)
            .AddAppDataSettings<ChatInteractionSettings>(configuration, AppDataConfigurationSections.ChatInteraction)
            .AddAppDataSettings<MemoryMetadata>(configuration, AppDataConfigurationSections.ChatInteractionMemory)
            .AddAppDataSettings<CopilotSettings>(configuration, AppDataConfigurationSections.Copilot)
            .AddAppDataSettings<McpServerOptions>(configuration, AppDataConfigurationSections.McpServer)
            .AddAppDataSettings<A2AHostOptions>(configuration, AppDataConfigurationSections.A2AHost)
            .AddAppDataSettings<PaginationSettings>(configuration, AppDataConfigurationSections.Pagination);
    }
}
