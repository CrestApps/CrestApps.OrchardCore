using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Certifications;
using CrestApps.OrchardCore.Learning.Credentials.Core.Indexes;
using CrestApps.OrchardCore.Learning.Credentials.Drivers;
using CrestApps.OrchardCore.Learning.Credentials.Migrations;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Credentials.Navigation;
using CrestApps.OrchardCore.Learning.Credentials.Rules;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using CrestApps.OrchardCore.Learning.Programs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Credentials;

/// <summary>
/// Registers Digital Credentials.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(CredentialsConstants.CollectionName));
        services.AddDataMigration<CredentialsMigrations>()
            .AddIndexProvider<CredentialAwardIndexProvider>();

        services.AddSigningKeys();
        services.AddLearningCredentialsCore();
        services.AddScoped<ICredentialDefinitionCatalog, ContentCredentialDefinitionCatalog>();
        services.AddContentPart<CredentialDefinitionPart>()
            .UseDisplayDriver<CredentialDefinitionPartDisplayDriver>();
        services.AddCredentialRuleKind<CourseCompletedRuleKind>();
        services.AddScoped<IAuthorizationHandler, CredentialDefinitionAuthorizationHandler>();
        services.AddPermissionProvider<CredentialsPermissionProvider>();
        services.AddNavigationProvider<CredentialsAdminMenu>();
    }
}

/// <summary>
/// The quiz passed rule, when Assessments is on.
/// </summary>
[RequireFeatures(AssessmentsConstants.Features.Assessments)]
public sealed class AssessmentsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddLearningCredentialsAssessments();
        services.AddCredentialRuleKind<QuizPassedRuleKind>();
    }
}

/// <summary>
/// The learning path completed rule, when Learning Paths is on.
/// </summary>
[RequireFeatures(ProgramsConstants.Features.Paths)]
public sealed class ProgramsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddLearningCredentialsPrograms();
        services.AddCredentialRuleKind<LearningPathCompletedRuleKind>();
    }
}

/// <summary>
/// The certification issued rule, when Learning Certifications is on.
/// </summary>
[RequireFeatures(CertificationsConstants.Features.Certifications)]
public sealed class CertificationsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddLearningCredentialsCertifications();
        services.AddCredentialRuleKind<CertificationIssuedRuleKind>();
    }
}
