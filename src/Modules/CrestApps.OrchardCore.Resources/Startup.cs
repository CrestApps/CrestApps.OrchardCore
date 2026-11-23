using CrestApps.OrchardCore.Parties;
using CrestApps.OrchardCore.PublicForms;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Resources.Endpoints;
using CrestApps.OrchardCore.Resources.PublicForms;
using CrestApps.OrchardCore.Resources.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Resources;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
        services.AddRecordDirectory();
        services.AddPartyDirectory();
    }

    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddRecordSearchEndpoint();
        routes.AddPartySearchEndpoint();
    }
}

/// <summary>
/// Makes content items a record kind, so modules can link to them.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Contents)]
public sealed class ContentsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecordSource<ContentItemRecordSource>();
    }
}

/// <summary>
/// Makes Orchard Core's ReCaptcha the challenge of every public form (<see cref="IPublicFormChallenge"/>), once its
/// keys are set.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.ReCaptcha)]
public sealed class ReCaptchaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPublicFormChallenge, ReCaptchaPublicFormChallenge>());
    }
}
