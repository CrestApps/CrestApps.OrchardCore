using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

internal sealed class ContactCenterEntryPointDisplayDriver : DisplayDriver<ContactCenterEntryPoint>
{
    private static readonly JsonSerializerOptions _ivrDisplayOptions = new(ContactCenterDeploymentSerializer.Options)
    {
        WriteIndented = true,
    };

    private readonly ContactCenterAdminFormOptionsProvider _optionsProvider;
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEntryPointDisplayDriver"/> class.
    /// </summary>
    /// <param name="optionsProvider">The admin form options provider.</param>
    /// <param name="siteService">The site service, which holds the approved external destinations.</param>
    public ContactCenterEntryPointDisplayDriver(
        ContactCenterAdminFormOptionsProvider optionsProvider,
        ISiteService siteService)
    {
        _optionsProvider = optionsProvider;
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(ContactCenterEntryPoint entryPoint, BuildDisplayContext context)
    {
        return CombineAsync(
            View("ContactCenterEntryPoint_Fields_SummaryAdmin", entryPoint)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
            View("ContactCenterEntryPoint_Buttons_SummaryAdmin", entryPoint)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5"),
            View("ContactCenterEntryPoint_DefaultMeta_SummaryAdmin", entryPoint)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
        );
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> EditAsync(ContactCenterEntryPoint entryPoint, BuildEditorContext context)
    {
        var viewModel = new EntryPointViewModel
        {
            Id = entryPoint.ItemId,
            Name = entryPoint.Name,
            Description = entryPoint.Description,
            DialedNumbersText = entryPoint.DialedNumbers is { Count: > 0 }
                ? string.Join(Environment.NewLine, entryPoint.DialedNumbers)
                : null,
            TargetType = entryPoint.TargetType,
            TargetAgentId = entryPoint.TargetAgentId,
            TargetQueueId = entryPoint.TargetQueueId,
            Priority = entryPoint.Priority,
            VoicemailEnabled = entryPoint.VoicemailEnabled,
            RingTimeoutSeconds = entryPoint.RingTimeoutSeconds,
            BusinessHoursCalendarId = entryPoint.BusinessHoursCalendarId,
            ClosedAction = entryPoint.ClosedAction,
            OverflowQueueId = entryPoint.OverflowQueueId,
            WelcomeMessage = entryPoint.WelcomeMessage,
            ClosedMessage = entryPoint.ClosedMessage,
            VoicemailGreetingText = entryPoint.VoicemailGreetingText,
            VoicemailRecipientAgentId = entryPoint.VoicemailRecipientAgentId,
            VoicemailDestination = entryPoint.VoicemailDestination,
            IvrFlowJson = entryPoint.IvrFlow is null
                ? null
                : JsonSerializer.Serialize(entryPoint.IvrFlow, _ivrDisplayOptions),
            Enabled = entryPoint.Enabled,
        };

        await _optionsProvider.PopulateEntryPointEditorAsync(viewModel);
        viewModel.IvrExternalDestinationOptions = await GetExternalDestinationOptionsAsync();
        viewModel.IvrVoiceMediaOptions = await _optionsProvider.GetVoiceMediaOptionsAsync(selectedMediaId: null);

        // The same agents a line can ring, as a separate list so the two pickers never share a selection.
        viewModel.VoicemailRecipientAgentOptions = viewModel.TargetAgentOptions?
            .Select(option => new SelectListItem(option.Text, option.Value) { Disabled = option.Disabled })
            .ToList() ?? [];

        // Grouped in cards by what they govern. Every card edits the same model under the same prefix, so the one form
        // still posts all of them together.
        void Populate(EntryPointViewModel model)
        {
            model.Id = viewModel.Id;
            model.Name = viewModel.Name;
            model.Description = viewModel.Description;
            model.DialedNumbersText = viewModel.DialedNumbersText;
            model.TargetType = viewModel.TargetType;
            model.TargetAgentId = viewModel.TargetAgentId;
            model.TargetAgentOptions = viewModel.TargetAgentOptions;
            model.TargetQueueId = viewModel.TargetQueueId;
            model.TargetQueueOptions = viewModel.TargetQueueOptions;
            model.Priority = viewModel.Priority;
            model.VoicemailEnabled = viewModel.VoicemailEnabled;
            model.RingTimeoutSeconds = viewModel.RingTimeoutSeconds;
            model.BusinessHoursCalendarId = viewModel.BusinessHoursCalendarId;
            model.BusinessHoursCalendarOptions = viewModel.BusinessHoursCalendarOptions;
            model.ClosedAction = viewModel.ClosedAction;
            model.OverflowQueueId = viewModel.OverflowQueueId;
            model.OverflowQueueOptions = viewModel.OverflowQueueOptions;
            model.WelcomeMessage = viewModel.WelcomeMessage;
            model.ClosedMessage = viewModel.ClosedMessage;
            model.VoicemailGreetingText = viewModel.VoicemailGreetingText;
            model.VoicemailRecipientAgentId = viewModel.VoicemailRecipientAgentId;
            model.VoicemailRecipientAgentOptions = viewModel.VoicemailRecipientAgentOptions;
            model.VoicemailDestination = viewModel.VoicemailDestination;
            model.IvrFlowJson = viewModel.IvrFlowJson;
            model.IvrQueueOptions = viewModel.IvrQueueOptions;
            model.IvrAgentOptions = viewModel.IvrAgentOptions;
            model.IvrExternalDestinationOptions = viewModel.IvrExternalDestinationOptions;
            model.IvrVoiceMediaOptions = viewModel.IvrVoiceMediaOptions;
            model.Enabled = viewModel.Enabled;
        }

        return Combine(
            Initialize<EntryPointViewModel>("ContactCenterEntryPointGeneral_Edit", Populate).Location("Content:1%General;1"),
            Initialize<EntryPointViewModel>("ContactCenterEntryPointRouting_Edit", Populate).Location("Content:1%Routing;2"),
            Initialize<EntryPointViewModel>("ContactCenterEntryPointHours_Edit", Populate).Location("Content:1%Hours;3"),
            Initialize<EntryPointViewModel>("ContactCenterEntryPointMenu_Edit", Populate).Location("Content:1%Welcome and IVR menu;4"),
            Initialize<EntryPointViewModel>("ContactCenterEntryPointVoicemail_Edit", Populate).Location("Content:1%Voicemail;5"));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ContactCenterEntryPoint entryPoint, UpdateEditorContext context)
    {
        var model = new EntryPointViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);


        var isAgentTarget = model.TargetType == CrestApps.OrchardCore.ContactCenter.Models.EntryPointTargetType.Agent;

        entryPoint.Name = model.Name?.Trim();
        entryPoint.Description = model.Description?.Trim();
        entryPoint.DialedNumbers = ParseLines(model.DialedNumbersText);
        entryPoint.TargetType = model.TargetType;

        // An entry point routes to a specific agent or a queue, never both: a specific-agent target rings that
        // agent directly and keeps no fallback queue, so the queue selection is cleared for an agent target and
        // the agent selection is cleared for a queue target.
        entryPoint.TargetAgentId = isAgentTarget && !string.IsNullOrWhiteSpace(model.TargetAgentId)
            ? model.TargetAgentId.Trim()
            : null;
        entryPoint.TargetQueueId = !isAgentTarget && !string.IsNullOrWhiteSpace(model.TargetQueueId)
            ? model.TargetQueueId.Trim()
            : null;

        // The rule that a routed-to target is required for the selected routing kind is enforced by
        // ContactCenterEntryPointHandler, so a recipe import and this editor reject the same entries.
        entryPoint.Priority = model.Priority;

        // Whether an unanswered specific-agent call goes to voicemail, and the ring window that governs it. The
        // window is always stored as a valid positive value; the checkbox controls whether it is used.
        entryPoint.VoicemailEnabled = model.VoicemailEnabled;
        entryPoint.RingTimeoutSeconds = model.RingTimeoutSeconds <= 0
            ? ContactCenterConstants.DirectRouting.DefaultRingTimeoutSeconds
            : Math.Clamp(
                model.RingTimeoutSeconds,
                ContactCenterConstants.DirectRouting.MinimumRingTimeoutSeconds,
                ContactCenterConstants.DirectRouting.MaximumRingTimeoutSeconds);

        entryPoint.BusinessHoursCalendarId = string.IsNullOrWhiteSpace(model.BusinessHoursCalendarId) ? null : model.BusinessHoursCalendarId.Trim();
        entryPoint.ClosedAction = model.ClosedAction;
        entryPoint.OverflowQueueId = string.IsNullOrWhiteSpace(model.OverflowQueueId) ? null : model.OverflowQueueId.Trim();
        entryPoint.WelcomeMessage = model.WelcomeMessage?.Trim();
        entryPoint.ClosedMessage = model.ClosedMessage?.Trim();
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
        entryPoint.Enabled = model.Enabled;

        // The menu tree is edited as JSON. The binder parses it and reports malformed JSON against the field;
        // whether the parsed flow is runnable is checked by the entry point handler, so a recipe import and
        // this editor reject the same flows.
        entryPoint.IvrFlow = model.IvrFlow;

        return await EditAsync(entryPoint, context);
    }

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

    private static List<string> ParseLines(string text)
    {
        var values = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return values;
        }

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.Length > 0 && !values.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                values.Add(trimmed);
            }
        }

        return values;
    }
}
