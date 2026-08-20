using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the health checks owned by the base Contact Center feature and maps the Contact Center
/// readiness and dependency probes, but only when the <c>OrchardCore.HealthChecks</c> feature is also
/// enabled so a deployment that does not use health checks never pays for them. The health-check options are
/// bound here for the same reason — nothing outside the health checks consumes them — so a deployment without
/// <c>OrchardCore.HealthChecks</c> neither binds nor validates them. The endpoints map here — rather than in
/// the base feature's <c>Configure</c> — because <c>MapHealthChecks</c> resolves the <c>HealthCheckService</c>
/// that only exists once <c>OrchardCore.HealthChecks</c> has registered it; mapping them unconditionally threw
/// at pipeline build time when health checks were not enabled.
/// </summary>
[RequireFeatures("OrchardCore.HealthChecks")]
public sealed class ContactCenterHealthChecksStartup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterHealthChecksStartup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration used to bind the health-check options.</param>
    public ContactCenterHealthChecksStartup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddOptions<ContactCenterHealthCheckOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps:ContactCenter:HealthChecks"))
            .Validate(
                options => options.DeadLetterDegradedThreshold >= 1,
                "'CrestApps:ContactCenter:HealthChecks:DeadLetterDegradedThreshold' must be at least one.")
            .Validate(
                options => options.OverdueBacklogDegradedThreshold >= 1,
                "'CrestApps:ContactCenter:HealthChecks:OverdueBacklogDegradedThreshold' must be at least one.")
            .Validate(
                options => options.ConsecutiveFailuresBeforeUnready >= 1,
                "'CrestApps:ContactCenter:HealthChecks:ConsecutiveFailuresBeforeUnready' must be at least one.")
            .Validate(
                options => options.ConsecutiveSuccessesBeforeReady >= 1,
                "'CrestApps:ContactCenter:HealthChecks:ConsecutiveSuccessesBeforeReady' must be at least one.")
            .Validate(
                options => options.DeadLetterUnhealthyThreshold >= options.DeadLetterDegradedThreshold,
                "'CrestApps:ContactCenter:HealthChecks:DeadLetterUnhealthyThreshold' cannot be lower than 'DeadLetterDegradedThreshold'.")
            .Validate(
                options => options.OverdueBacklogUnhealthyThreshold >= options.OverdueBacklogDegradedThreshold,
                "'CrestApps:ContactCenter:HealthChecks:OverdueBacklogUnhealthyThreshold' cannot be lower than 'OverdueBacklogDegradedThreshold'.")
            .ValidateOnStart();

        services.AddContactCenterHealthChecks();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddContactCenterHealthEndpoints();
    }
}
