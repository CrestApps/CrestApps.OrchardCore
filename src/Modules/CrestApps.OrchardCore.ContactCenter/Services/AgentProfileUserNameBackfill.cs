using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Fills in the user name on agent profiles saved without one, once the tenant has started.
/// </summary>
/// <remarks>
/// Saving a profile runs <see cref="Handlers.AgentProfileUserNameHandler"/>, which reads the name from the user
/// account. Only profiles missing a name are saved, so a tenant whose profiles are complete does no writes. The work
/// is deferred until activation has finished: run during activation, the scope it needs waited on the very tenant
/// that was still starting, and the tenant never finished starting.
/// </remarks>
internal sealed class AgentProfileUserNameBackfill : ModularTenantEvents
{
    private readonly ILogger _logger;

    public AgentProfileUserNameBackfill(ILogger<AgentProfileUserNameBackfill> logger)
    {
        _logger = logger;
    }

    public override Task ActivatedAsync()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            try
            {
                var manager = scope.ServiceProvider.GetRequiredService<IAgentProfileManager>();

                foreach (var profile in await manager.GetAllAsync())
                {
                    if (string.IsNullOrEmpty(profile.UserName) && !string.IsNullOrEmpty(profile.UserId))
                    {
                        await manager.UpdateAsync(profile);
                    }
                }
            }
            catch (Exception ex)
            {
                // A name missing from a report is not a reason to disturb the tenant.
                _logger.LogWarning(ex, "Could not fill in the user names of agent profiles saved without one.");
            }
        });

        return Task.CompletedTask;
    }
}
