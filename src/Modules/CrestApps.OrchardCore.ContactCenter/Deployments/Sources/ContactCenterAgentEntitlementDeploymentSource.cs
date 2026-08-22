using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Deployment;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Sources;

/// <summary>
/// Exports the manager-owned, environment-portable configuration of every agent profile, keyed by the Orchard user
/// name so the plan resolves to the matching user in the target environment. Live runtime presence state and the
/// environment-specific user and item identifiers are deliberately omitted.
/// </summary>
internal sealed class ContactCenterAgentEntitlementDeploymentSource : DeploymentSourceBase<ContactCenterAgentEntitlementDeploymentStep>
{
    private readonly IAgentProfileManager _manager;
    private readonly UserManager<IUser> _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterAgentEntitlementDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the agent profiles.</param>
    /// <param name="userManager">The Orchard user manager used to resolve the current user name for each profile.</param>
    public ContactCenterAgentEntitlementDeploymentSource(
        IAgentProfileManager manager,
        UserManager<IUser> userManager)
    {
        _manager = manager;
        _userManager = userManager;
    }

    protected override async Task ProcessAsync(ContactCenterAgentEntitlementDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = await _manager.GetAllAsync();

        var data = new JsonArray();

        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.UserId))
            {
                continue;
            }

            var user = await _userManager.FindByIdAsync(entry.UserId);

            if (user is null)
            {
                continue;
            }

            var userName = await _userManager.GetUserNameAsync(user);

            if (string.IsNullOrEmpty(userName))
            {
                continue;
            }

            data.Add(new JsonObject
            {
                ["UserName"] = userName,
                ["DisplayName"] = entry.DisplayName,
                ["MaxConcurrentInteractions"] = entry.MaxConcurrentInteractions,
                ["AllowedQueueIds"] = ToJsonArray(entry.AllowedQueueIds),
                ["AllowedCampaignIds"] = ToJsonArray(entry.AllowedCampaignIds),
                ["Skills"] = ToJsonArray(entry.Skills),
            });
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = ContactCenterDeploymentSteps.AgentEntitlement,
            ["Agents"] = data,
        });
    }

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();

        if (values is not null)
        {
            foreach (var value in values)
            {
                array.Add(value);
            }
        }

        return array;
    }
}
