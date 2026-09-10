using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Filters;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the CRM-integrated agent desktop and its navigation and state endpoints. The desktop is
/// integration glue rather than a separately selectable feature: it activates automatically whenever the
/// agents, real-time transport, voice, and Telephony soft-phone capabilities it composes are all enabled.
/// The soft phone itself remains capability-gated at request time by the Telephony soft-phone widget, so a
/// provider without in-browser audio (for example Dialpad) still gets the provider-neutral workspace.
/// </summary>
[RequireFeatures(
    ContactCenterConstants.Feature.Agents,
    ContactCenterConstants.Feature.RealTime,
    ContactCenterConstants.Feature.Voice,
    TelephonyConstants.Feature.SoftPhoneCore)]
public sealed class AgentDesktopStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNavigationProvider<ContactCenterAgentDesktopAdminMenu>();

        // The docked agent bar is the CRM-side bridge to the call router. It rides the admin chrome on every page
        // so an assignment reaches the agent wherever they are, even when the soft phone runs in its own window or
        // the browser extension. It reuses the workspace state and offer endpoints registered above.
        services.AddScoped<IContactCenterAgentBarBuilder, ContactCenterAgentBarBuilder>();
        services.Configure<MvcOptions>(options =>
        {
            options.Filters.Add<ContactCenterAgentBarFilter>();
        });
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddAgentWorkspaceEndpoints();
    }
}
