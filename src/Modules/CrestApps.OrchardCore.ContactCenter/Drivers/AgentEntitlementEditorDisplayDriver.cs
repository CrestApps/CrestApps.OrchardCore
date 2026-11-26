using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

/// <summary>
/// Builds the editor of the agent entitlements page as cards: who the agent is, the queues they may serve, the
/// campaigns they may work and their skills.
/// </summary>
internal sealed class AgentEntitlementEditorDisplayDriver : DisplayDriver<AgentEntitlementViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentEntitlementEditorDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AgentEntitlementEditorDisplayDriver(IStringLocalizer<AgentEntitlementEditorDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(AgentEntitlementViewModel model, BuildEditorContext context)
    {
        // Every card edits a copy of the same model under the same prefix, so the form keeps posting the names it always did.
        void Populate(AgentEntitlementViewModel target)
        {
            target.Id = model.Id;
            target.UserName = model.UserName;
            target.UserDisplayName = model.UserDisplayName;
            target.UserSearchEndpoint = model.UserSearchEndpoint;
            target.AllowedQueueIds = model.AllowedQueueIds;
            target.QueueOptions = model.QueueOptions;
            target.AllowedCampaignIds = model.AllowedCampaignIds;
            target.CampaignOptions = model.CampaignOptions;
            target.SkillProficiencies = model.SkillProficiencies;
            target.SkillOptions = model.SkillOptions;
            target.QueueMemberships = model.QueueMemberships;
        }

        // The agent is chosen once, when the entitlements are created; afterwards they are shown but not changed.
        var user = context.IsNew
            ? Initialize<AgentEntitlementViewModel>("AgentEntitlementUser_Edit", Populate)
                .Location(l => l.Zone("Content", "1").Card(S["Agent"], "10"))
            : Initialize<AgentEntitlementViewModel>("AgentEntitlementUserReadOnly_Edit", Populate)
                .Location(l => l.Zone("Content", "1").Card(S["Agent"], "10"));

        return Combine(
            user,
            Initialize<AgentEntitlementViewModel>("AgentEntitlementQueues_Edit", Populate)
                .Location(l => l.Zone("Content", "2").Card(S["Queues"], "20")),
            Initialize<AgentEntitlementViewModel>("AgentEntitlementCampaigns_Edit", Populate)
                .Location(l => l.Zone("Content", "3").Card(S["Campaigns"], "30")),
            Initialize<AgentEntitlementViewModel>("AgentEntitlementSkills_Edit", Populate)
                .Location(l => l.Zone("Content", "4").Card(S["Skills"], "40")));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(AgentEntitlementViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            m => m.AllowedQueueIds,
            m => m.QueueMemberships,
            m => m.AllowedCampaignIds,
            m => m.SkillProficiencies);

        if (context.IsNew)
        {
            await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.UserName);
        }

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(AgentEntitlementViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }
}
