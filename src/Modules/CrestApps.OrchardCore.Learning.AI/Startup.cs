using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.DataSources;
using CrestApps.OrchardCore.Learning.AI.Drivers;
using CrestApps.OrchardCore.Learning.AI.Handlers;
using CrestApps.OrchardCore.Learning.AI.Migrations;
using CrestApps.OrchardCore.Learning.AI.Models;
using CrestApps.OrchardCore.Learning.AI.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.AI;

/// <summary>
/// Registers the learning assistant: the enrollment retrieval filter, the tutor profiles and their template, the
/// course switch, and the readers behind the Learning AI tools.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddOptions<LearningAIOptions>();
        services.AddScoped<LearningTutorProfiles>();
        services.AddAIDataSourceResultFilter<LearningRetrievalFilter>();
        services.AddScoped<LearnerLearningReader>();
        services.AddScoped<LearnerRecordsLister>();

        services.AddContentPart<LearningAssistantPart>()
            .UseDisplayDriver<LearningAssistantPartDisplayDriver>();
        services.AddDataMigration<LearningAIMigrations>();
        services.AddDisplayDriver<AIProfile, LearningTutorProfileDisplayDriver>();
        services.AddScoped<IAIProfileTemplateApplicationHandler, LearningTutorTemplateApplicationHandler>();
    }
}
