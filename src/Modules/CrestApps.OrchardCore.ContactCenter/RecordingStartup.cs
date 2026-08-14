using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers voice interaction recording orchestration.
/// </summary>
[Feature(ContactCenterConstants.Feature.Recording)]
public sealed class RecordingStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecordingGovernancePolicy, RecordingGovernancePolicy>();
        services.AddScoped<IContactCenterRecordingService, ContactCenterRecordingService>();
        services.AddScoped<IAgentRecordingControlService, AgentRecordingControlService>();
        services.AddScoped<ISecurePauseAutoResumeService, SecurePauseAutoResumeService>();
        services.AddScoped<IRecordingAccessGovernanceService, RecordingAccessGovernanceService>();
        services.AddScoped<IContactCenterEventHandler, RecordingMediaDeletionHandler>();
        services.AddScoped<IRecordingErasureGuard, RecordingErasureGuard>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, SecurePauseAutoResumeBackgroundTask>());
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        var adminOptions = serviceProvider.GetRequiredService<IOptions<AdminOptions>>().Value;
        routes.AddRecordingErasureEndpoint(adminOptions.AdminUrlPrefix);
    }
}
