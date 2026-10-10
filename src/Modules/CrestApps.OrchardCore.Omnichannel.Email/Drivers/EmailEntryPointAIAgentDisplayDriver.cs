using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Email.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Email.Drivers;

/// <summary>
/// Adds the AI agent picker to an email entry point's routing card: when the entry point routes to an AI agent, the
/// chosen chat profile answers the customer's first email and holds the conversation.
/// </summary>
internal sealed class EmailEntryPointAIAgentDisplayDriver : DisplayDriver<ContactCenterEntryPoint>
{
    private readonly IAIProfileManager _profileManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailEntryPointAIAgentDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileManager">The AI profile manager.</param>
    public EmailEntryPointAIAgentDisplayDriver(IAIProfileManager profileManager)
    {
        _profileManager = profileManager;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ContactCenterEntryPoint entryPoint, BuildEditorContext context)
    {
        if (!AnswersEmail(entryPoint))
        {
            return null;
        }

        return Initialize<EmailEntryPointAIAgentViewModel>("EmailEntryPointAIAgent_Edit", async model =>
        {
            model.TargetAIProfileId = entryPoint.TargetAIProfileId;

            var profiles = await _profileManager.GetAsync(AIProfileType.Chat);

            model.ProfileOptions = profiles
                .OrderBy(profile => profile.DisplayText ?? profile.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(profile => new SelectListItem(
                    profile.DisplayText ?? profile.Name,
                    profile.ItemId,
                    string.Equals(profile.ItemId, entryPoint.TargetAIProfileId, StringComparison.Ordinal)))
                .ToList();
        }).Location("Content:2.1%Routing;2");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ContactCenterEntryPoint entryPoint, UpdateEditorContext context)
    {
        if (!AnswersEmail(entryPoint))
        {
            return null;
        }

        var model = new EmailEntryPointAIAgentViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // Kept only for an AI agent target; the entry point handler requires it when the target is an AI agent.
        entryPoint.TargetAIProfileId = model.TargetType == EntryPointTargetType.AIAgent && !string.IsNullOrWhiteSpace(model.TargetAIProfileId)
            ? model.TargetAIProfileId.Trim()
            : null;

        return Edit(entryPoint, context);
    }

    private static bool AnswersEmail(ContactCenterEntryPoint entryPoint)
        => string.Equals(entryPoint.GetChannel(), OmnichannelConstants.Channels.Email, StringComparison.OrdinalIgnoreCase);
}
