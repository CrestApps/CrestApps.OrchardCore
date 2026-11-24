using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.ContactCenter.Staff;

/// <summary>
/// Makes the person behind a new agent profile a staff member with the Agent capability. It runs after the request
/// ends, so an agent signing in never waits for it, and a failure never blocks the sign-in.
/// </summary>
internal sealed class AgentProfileStaffHandler : CatalogEntryHandlerBase<AgentProfile>
{
    public override Task CreatedAsync(CreatedContext<AgentProfile> context, CancellationToken cancellationToken = default)
    {
        var agent = context.Model;

        if (string.IsNullOrEmpty(agent.UserId))
        {
            return Task.CompletedTask;
        }

        var userId = agent.UserId;
        var name = string.IsNullOrWhiteSpace(agent.DisplayName) ? agent.UserName : agent.DisplayName;

        ShellScope.AddDeferredTask(async scope =>
        {
            try
            {
                await scope.ServiceProvider.GetRequiredService<IStaffAccountService>().EnsureAsync(userId, name ?? userId, [StaffStartup.AgentCapability], staff => staff.JobTitle ??= "Agent");
            }
            catch (Exception ex)
            {
                scope.ServiceProvider.GetRequiredService<ILogger<AgentProfileStaffHandler>>().LogError(ex, "The new agent {UserId} could not be made a staff member.", userId.SanitizeLogValue());
            }
        });

        return Task.CompletedTask;
    }
}
