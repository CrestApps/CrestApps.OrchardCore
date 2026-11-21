using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Learning.Assessments.ContentTransfer.Moodle;
using CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Deployments;
using CrestApps.OrchardCore.Learning.Assessments.Drivers;
using CrestApps.OrchardCore.Learning.Assessments.Migrations;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Navigation;
using CrestApps.OrchardCore.Learning.Assessments.Recipes;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Assessments;

/// <summary>
/// Registers the quiz course item, whose questions, attempts and grading live in a survey the quiz owns, and the
/// Learning grading page.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<AssessmentsMigrations>()
            .AddIndexProvider<GradeScaleIndexProvider>();

        services.AddLearningAssessmentsCore();
        services.AddContentPart<QuizPart>()
            .UseDisplayDriver<QuizPartDisplayDriver>()
            .UseDisplayDriver<QuizPartLearnerDisplayDriver>()
            .AddHandler<QuizPartHandler>();

        services.AddDisplayDriver<GradingQueueRow, GradingQueueRowDisplayDriver>();
        services.AddPermissionProvider<AssessmentsPermissionsProvider>();
        services.AddNavigationProvider<AssessmentsAdminMenu>();
    }
}

/// <summary>
/// Registers the grade scales recipe step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class AssessmentsRecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<LearnerUserLookup>();
        services.AddRecipeExecutionStep<LearningGradeScalesRecipeStep>();
    }
}

/// <summary>
/// Registers the grade scales deployment step.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class AssessmentsDeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<LearningGradeScalesDeploymentSource, LearningGradeScalesDeploymentStep, LearningGradeScalesDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Registers the schemas of the grade scales recipe and deployment steps.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class AssessmentsRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, LearningGradeScalesRecipeStepSchema>();
        services.AddDeploymentStepSchema<LearningGradeScalesDeploymentStepSchema>();
    }
}

/// <summary>
/// Registers the Moodle course backup import.
/// </summary>
[Feature(AssessmentsConstants.Features.MoodleImport)]
public sealed class MoodleImportStartup : StartupBase
{
    private readonly IShellConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="MoodleImportStartup"/> class.
    /// </summary>
    /// <param name="configuration">The tenant configuration.</param>
    public MoodleImportStartup(IShellConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<MoodleImportOptions>(_configuration.GetSection("CrestApps:Learning:MoodleImport"));
        services.AddSingleton<MoodleWorkspaces>();
        services.AddSingleton<IContentTransferFileFormatProvider, MoodleFileFormatProvider>();
        services.AddScoped<IContentImportHandler, MoodleImportHandler>();
        services.AddScoped<IContentImportRowFilter, MoodleSourceRowFilter>();
        services.AddScoped<IContentImportRowTypeProvider, MoodleRowTypeProvider>();
    }
}
