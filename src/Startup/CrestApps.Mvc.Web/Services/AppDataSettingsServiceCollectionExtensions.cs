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

        return services
            .AddAppDataSettings<GeneralAISettings>(configuration, AppDataConfigurationSections.GeneralAISettings)
            .AddAppDataSettings<DefaultOrchestratorSettings>(configuration, AppDataConfigurationSections.DefaultOrchestrator)
            .AddAppDataSettings<DefaultAIDeploymentSettings>(configuration, AppDataConfigurationSections.DefaultDeployments)
            .AddAppDataSettings<AIMemorySettings>(configuration, AppDataConfigurationSections.AIMemory)
            .AddAppDataSettings<InteractionDocumentSettings>(configuration, AppDataConfigurationSections.InteractionDocuments)
            .AddAppDataSettings<AIDataSourceSettings>(configuration, AppDataConfigurationSections.AIDataSources)
            .AddAppDataSettings<ChatInteractionSettings>(configuration, AppDataConfigurationSections.ChatInteraction)
            .AddAppDataSettings<ChatInteractionMemorySettings>(configuration, AppDataConfigurationSections.ChatInteractionMemory)
            .AddAppDataSettings<CopilotSettings>(configuration, AppDataConfigurationSections.Copilot)
            .AddAppDataSettings<McpServerOptions>(configuration, AppDataConfigurationSections.McpServer)
            .AddAppDataSettings<PaginationSettings>(configuration, AppDataConfigurationSections.Pagination);
    }
}
