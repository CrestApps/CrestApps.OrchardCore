using CrestApps.OrchardCore.AI.Core.DataSources;
using CrestApps.OrchardCore.Learning.AI.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.AI;

/// <summary>
/// Registers the learning assistant: the enrollment retrieval filter and the tutor profiles.
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
    }
}
