using CrestApps.OrchardCore.Surveys.Drivers;
using CrestApps.OrchardCore.Surveys.Migrations;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using Shortcodes;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Registers the Survey part and widget.
/// </summary>
[Feature(SurveysConstants.Features.Embedding)]
public sealed class EmbeddingStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<SurveyEmbedResolver>();
        services.AddContentPart<SurveyPart>()
            .UseDisplayDriver<SurveyPartDisplayDriver>();
        services.AddDataMigration<SurveyEmbeddingMigrations>();
    }
}

/// <summary>
/// Registers the survey shortcode.
/// </summary>
[Feature(SurveysConstants.Features.Embedding)]
[RequireFeatures(OrchardCoreFeatureIds.Shortcodes)]
public sealed class EmbeddingShortcodesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.TryAddEnumerable(ServiceDescriptor.Scoped<IShortcodeProvider, SurveyShortcodeProvider>());
}
