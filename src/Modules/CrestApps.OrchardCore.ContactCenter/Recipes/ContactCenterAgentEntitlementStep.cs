using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.ContactCenter.Recipes;

/// <summary>
/// Imports manager-owned agent entitlements carried by a recipe step. Each entry is matched to a target user by user
/// name; new agent profiles are created and existing profiles have their manager-owned configuration promoted without
/// disturbing live runtime presence state.
/// </summary>
internal sealed class ContactCenterAgentEntitlementStep : NamedRecipeStepHandler
{
    private readonly IAgentProfileManager _agentManager;
    private readonly IAgentPresenceManager _presenceManager;
    private readonly ContactCenterAdminFormOptionsProvider _optionsProvider;
    private readonly UserManager<IUser> _userManager;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterAgentEntitlementStep"/> class.
    /// </summary>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="presenceManager">The agent presence manager that promotes configuration.</param>
    /// <param name="optionsProvider">The Contact Center form options provider used to filter dangling references.</param>
    /// <param name="userManager">The Orchard user manager used to resolve users by name.</param>
    /// <param name="clock">The clock used to stamp new agent profiles.</param>
    /// <param name="stringLocalizer">The string localizer for error messages.</param>
    public ContactCenterAgentEntitlementStep(
        IAgentProfileManager agentManager,
        IAgentPresenceManager presenceManager,
        ContactCenterAdminFormOptionsProvider optionsProvider,
        UserManager<IUser> userManager,
        IClock clock,
        IStringLocalizer<ContactCenterAgentEntitlementStep> stringLocalizer)
        : base(ContactCenterDeploymentSteps.AgentEntitlement)
    {
        _agentManager = agentManager;
        _presenceManager = presenceManager;
        _optionsProvider = optionsProvider;
        _userManager = userManager;
        _clock = clock;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<ContactCenterAgentEntitlementStepModel>();
        var tokens = model.Agents?.OfType<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            var entry = token.ToObject<AgentEntitlementImportModel>();

            if (string.IsNullOrWhiteSpace(entry.UserName))
            {
                context.Errors.Add(S["A Contact Center agent entitlement entry is missing a user name and was skipped."]);

                continue;
            }

            var user = await _userManager.FindByNameAsync(entry.UserName.Trim());

            if (user is null)
            {
                context.Errors.Add(S["No Orchard user named '{0}' was found for the Contact Center agent entitlement entry.", entry.UserName]);

                continue;
            }

            var userId = await _userManager.GetUserIdAsync(user);
            var userName = await _userManager.GetUserNameAsync(user);

            var allowedQueueIds = await _optionsProvider.FilterExistingQueueIdsAsync(entry.AllowedQueueIds);
            var allowedCampaignIds = await _optionsProvider.FilterExistingCampaignIdsAsync(entry.AllowedCampaignIds);

            var configuration = new AgentManagedConfiguration
            {
                DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? userName : entry.DisplayName,
                MaxConcurrentInteractions = entry.MaxConcurrentInteractions < 1 ? 1 : entry.MaxConcurrentInteractions,
                AllowedQueueIds = allowedQueueIds,
                AllowedCampaignIds = allowedCampaignIds,
                Skills = entry.Skills,
            };

            var existing = await _agentManager.FindByUserIdAsync(userId);

            if (existing is not null)
            {
                await _presenceManager.ApplyManagedConfigurationAsync(existing.ItemId, configuration);

                continue;
            }

            var agent = await _agentManager.NewAsync();
            agent.UserId = userId;
            agent.UserName = userName;
            agent.Name = userId;
            agent.DisplayName = configuration.DisplayName;
            agent.MaxConcurrentInteractions = configuration.MaxConcurrentInteractions;
            agent.AllowedQueueIds = AgentEntitlementUtilities.NormalizeIds(allowedQueueIds);
            agent.AllowedCampaignIds = AgentEntitlementUtilities.NormalizeIds(allowedCampaignIds);
            agent.Skills = AgentEntitlementUtilities.NormalizeIds(entry.Skills);
            agent.CreatedUtc = _clock.UtcNow;

            var validationResult = await _agentManager.ValidateAsync(agent);

            if (!validationResult.Succeeded)
            {
                foreach (var error in validationResult.Errors)
                {
                    context.Errors.Add(error.ErrorMessage);
                }

                continue;
            }

            await _agentManager.CreateAsync(agent);
        }
    }

    private sealed class ContactCenterAgentEntitlementStepModel
    {
        public JsonArray Agents { get; set; }
    }

    private sealed class AgentEntitlementImportModel
    {
        public string UserName { get; set; }

        public string DisplayName { get; set; }

        public int MaxConcurrentInteractions { get; set; } = 1;

        public IList<string> AllowedQueueIds { get; set; }

        public IList<string> AllowedCampaignIds { get; set; }

        public IList<string> Skills { get; set; }
    }
}
