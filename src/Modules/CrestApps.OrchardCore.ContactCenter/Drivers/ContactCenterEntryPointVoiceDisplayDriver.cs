using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Core;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

/// <summary>
/// Adds what only an entry point that answers calls has: how long an agent's line rings before voicemail, the welcome
/// message and phone menu, and where voicemails go.
/// </summary>
internal sealed class ContactCenterEntryPointVoiceDisplayDriver : DisplayDriver<ContactCenterEntryPoint>
{
    private static readonly JsonSerializerOptions _ivrDisplayOptions = new(ContactCenterDeploymentSerializer.Options)
    {
        WriteIndented = true,
    };

    private readonly ContactCenterAdminFormOptionsProvider _optionsProvider;
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEntryPointVoiceDisplayDriver"/> class.
    /// </summary>
    /// <param name="optionsProvider">The admin form options provider.</param>
    /// <param name="siteService">The site service, which holds the approved external destinations.</param>
    public ContactCenterEntryPointVoiceDisplayDriver(
        ContactCenterAdminFormOptionsProvider optionsProvider,
        ISiteService siteService)
    {
        _optionsProvider = optionsProvider;
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> EditAsync(ContactCenterEntryPoint entryPoint, BuildEditorContext context)
    {
        if (!AnswersCalls(entryPoint))
        {
            return null;
        }

        var viewModel = new EntryPointViewModel
        {
            Id = entryPoint.ItemId,
            TargetType = entryPoint.TargetType,
            TargetAgentId = entryPoint.TargetAgentId,
            VoicemailEnabled = entryPoint.VoicemailEnabled,
            RingTimeoutSeconds = entryPoint.RingTimeoutSeconds,
            WelcomeMessage = entryPoint.WelcomeMessage,
            VoicemailGreetingText = entryPoint.VoicemailGreetingText,
            VoicemailRecipientAgentId = entryPoint.VoicemailRecipientAgentId,
            VoicemailDestination = entryPoint.VoicemailDestination,
            IvrFlowJson = entryPoint.IvrFlow is null
                ? null
                : JsonSerializer.Serialize(entryPoint.IvrFlow, _ivrDisplayOptions),
        };

        await _optionsProvider.PopulateEntryPointEditorAsync(viewModel);
        viewModel.IvrExternalDestinationOptions = await GetExternalDestinationOptionsAsync();
        viewModel.IvrVoiceMediaOptions = await _optionsProvider.GetVoiceMediaOptionsAsync(selectedMediaId: null);

        // The same agents a line can ring, as a separate list so the two pickers never share a selection.
        viewModel.VoicemailRecipientAgentOptions = viewModel.TargetAgentOptions?
            .Select(option => new SelectListItem(option.Text, option.Value) { Disabled = option.Disabled })
            .ToList() ?? [];

        void Populate(EntryPointViewModel model)
        {
            model.Id = viewModel.Id;
            model.TargetType = viewModel.TargetType;
            model.VoicemailEnabled = viewModel.VoicemailEnabled;
            model.RingTimeoutSeconds = viewModel.RingTimeoutSeconds;
            model.WelcomeMessage = viewModel.WelcomeMessage;
            model.VoicemailGreetingText = viewModel.VoicemailGreetingText;
            model.VoicemailRecipientAgentId = viewModel.VoicemailRecipientAgentId;
            model.VoicemailRecipientAgentOptions = viewModel.VoicemailRecipientAgentOptions;
            model.VoicemailDestination = viewModel.VoicemailDestination;
            model.IvrFlowJson = viewModel.IvrFlowJson;
            model.IvrQueueOptions = viewModel.IvrQueueOptions;
            model.IvrAgentOptions = viewModel.IvrAgentOptions;
            model.IvrExternalDestinationOptions = viewModel.IvrExternalDestinationOptions;
            model.IvrVoiceMediaOptions = viewModel.IvrVoiceMediaOptions;
        }

        return Combine(
            Initialize<EntryPointViewModel>("ContactCenterEntryPointAgentRing_Edit", Populate).Location("Content:2%Routing;2"),
            Initialize<EntryPointViewModel>("ContactCenterEntryPointMenu_Edit", Populate).Location("Content:1%Welcome and IVR menu;4"),
            Initialize<EntryPointViewModel>("ContactCenterEntryPointVoicemail_Edit", Populate).Location("Content:1%Voicemail;5"));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ContactCenterEntryPoint entryPoint, UpdateEditorContext context)
    {
        if (!AnswersCalls(entryPoint))
        {
            return null;
        }

        var model = new EntryPointViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var isAgentTarget = model.TargetType == EntryPointTargetType.Agent;

        // Whether an unanswered specific-agent call goes to voicemail, and the ring window that governs it. The
        // window is always stored as a valid positive value; the checkbox controls whether it is used.
        entryPoint.VoicemailEnabled = model.VoicemailEnabled;
        entryPoint.RingTimeoutSeconds = model.RingTimeoutSeconds <= 0
            ? ContactCenterConstants.DirectRouting.DefaultRingTimeoutSeconds
            : Math.Clamp(
                model.RingTimeoutSeconds,
                ContactCenterConstants.DirectRouting.MinimumRingTimeoutSeconds,
                ContactCenterConstants.DirectRouting.MaximumRingTimeoutSeconds);

        entryPoint.WelcomeMessage = model.WelcomeMessage?.Trim();
        entryPoint.VoicemailGreetingText = string.IsNullOrWhiteSpace(model.VoicemailGreetingText) ? null : model.VoicemailGreetingText.Trim();

        // Only a queue line needs a mailbox of its own; a personal line's messages always go to its agent. A line that
        // delivers to the queue's shared box has no agent inbox, so the agent selection is cleared rather than kept
        // looking as if it still received the line's messages.
        var deliversToSharedBox = !isAgentTarget && model.VoicemailDestination == EntryPointVoicemailDestination.QueueSharedBox;

        entryPoint.VoicemailDestination = deliversToSharedBox
            ? EntryPointVoicemailDestination.QueueSharedBox
            : EntryPointVoicemailDestination.AgentInbox;
        entryPoint.VoicemailRecipientAgentId = !isAgentTarget && !deliversToSharedBox && !string.IsNullOrWhiteSpace(model.VoicemailRecipientAgentId)
            ? model.VoicemailRecipientAgentId.Trim()
            : null;

        // The menu tree is edited as JSON. The binder parses it and reports malformed JSON against the field;
        // whether the parsed flow is runnable is checked by the entry point handler, so a recipe import and
        // this editor reject the same flows.
        entryPoint.IvrFlow = model.IvrFlow;

        return await EditAsync(entryPoint, context);
    }

    private static bool AnswersCalls(ContactCenterEntryPoint entryPoint)
        => string.Equals(entryPoint.GetChannel(), OmnichannelConstants.Channels.Phone, StringComparison.OrdinalIgnoreCase);

    // The approved destinations an IVR external transfer may name. A disabled one is still listed, marked, so a
    // menu that names it reads as a choice to revisit rather than an unknown identifier.
    private async Task<IList<SelectListItem>> GetExternalDestinationOptionsAsync()
    {
        var site = await _siteService.GetSiteSettingsAsync();
        var settings = site.GetOrCreate<ContactCenterExternalTransferSettings>();

        return settings.Destinations
            .Where(destination => destination is not null && !string.IsNullOrWhiteSpace(destination.Id))
            .Select(destination => new SelectListItem(DescribeDestination(destination), destination.Id) { Disabled = !destination.Enabled })
            .OrderBy(option => option.Text, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string DescribeDestination(ContactCenterExternalDestination destination)
    {
        var name = string.IsNullOrWhiteSpace(destination.DisplayName) ? destination.Id : destination.DisplayName;

        return string.IsNullOrWhiteSpace(destination.E164Address)
            ? name
            : $"{name} ({destination.E164Address})";
    }
}
