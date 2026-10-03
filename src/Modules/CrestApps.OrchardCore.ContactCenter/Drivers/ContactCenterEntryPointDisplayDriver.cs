using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

/// <summary>
/// Edits what every entry point has, whatever channel it answers: its name, the numbers it picks from the address
/// list, where it routes to, and its opening hours. The features that answer a channel add that channel's own cards,
/// such as the phone menu and voicemail of a call entry point.
/// </summary>
internal sealed class ContactCenterEntryPointDisplayDriver : DisplayDriver<ContactCenterEntryPoint>
{
    private readonly ContactCenterAdminFormOptionsProvider _optionsProvider;
    private readonly IOmnichannelChannelEndpointManager _addressManager;
    private readonly EntryPointChannelOptions _channelOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEntryPointDisplayDriver"/> class.
    /// </summary>
    /// <param name="optionsProvider">The admin form options provider.</param>
    /// <param name="addressManager">The address list the entry point picks its numbers from.</param>
    /// <param name="channelOptions">The channels entry points can answer.</param>
    public ContactCenterEntryPointDisplayDriver(
        ContactCenterAdminFormOptionsProvider optionsProvider,
        IOmnichannelChannelEndpointManager addressManager,
        IOptions<EntryPointChannelOptions> channelOptions)
    {
        _optionsProvider = optionsProvider;
        _addressManager = addressManager;
        _channelOptions = channelOptions.Value;
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
        var channel = entryPoint.GetChannel();
        var addresses = await _addressManager.GetAllAsync();

        // A stored id may be one a merged address absorbed; it is shown, and saved, as the address it became.
        var selectedIds = (entryPoint.AddressIds ?? [])
            .Select(id => addresses.FirstOrDefault(address => address.IsKnownAs(id))?.ItemId ?? id)
            .ToHashSet(StringComparer.Ordinal);

        var viewModel = new EntryPointViewModel
        {
            Id = entryPoint.ItemId,
            Name = entryPoint.Name,
            Description = entryPoint.Description,
            Channel = channel,
            ChannelDisplayName = _channelOptions.Channels.TryGetValue(channel, out var registered) && registered.DisplayName is not null
                ? registered.DisplayName.Value
                : channel,
            AddressIds = [.. selectedIds],
            AddressOptions = addresses
                .Where(address => address.HasCapability(channel) || selectedIds.Contains(address.ItemId))
                .OrderBy(address => address.DisplayText, StringComparer.CurrentCultureIgnoreCase)
                .Select(address => new SelectListItem(
                    string.IsNullOrWhiteSpace(address.DisplayText) || address.DisplayText == address.Value ? address.Value : $"{address.DisplayText} ({address.Value})",
                    address.ItemId,
                    selectedIds.Contains(address.ItemId)))
                .ToList(),
            LegacyDialedNumbers = [.. (entryPoint.DialedNumbers ?? []).Where(number => !string.IsNullOrWhiteSpace(number))],
            TargetType = entryPoint.TargetType,
            TargetAgentId = entryPoint.TargetAgentId,
            TargetQueueId = entryPoint.TargetQueueId,
            Priority = entryPoint.Priority,
            BusinessHoursCalendarId = entryPoint.BusinessHoursCalendarId,
            ClosedAction = entryPoint.ClosedAction,
            OverflowQueueId = entryPoint.OverflowQueueId,
            ClosedMessage = entryPoint.ClosedMessage,
            Enabled = entryPoint.Enabled,
        };

        await _optionsProvider.PopulateEntryPointEditorAsync(viewModel);

        // Grouped in cards by what they govern. Every card edits the same model under the same prefix, so the one form
        // still posts all of them together.
        void Populate(EntryPointViewModel model)
        {
            model.Id = viewModel.Id;
            model.Name = viewModel.Name;
            model.Description = viewModel.Description;
            model.Channel = viewModel.Channel;
            model.ChannelDisplayName = viewModel.ChannelDisplayName;
            model.AddressIds = viewModel.AddressIds;
            model.AddressOptions = viewModel.AddressOptions;
            model.LegacyDialedNumbers = viewModel.LegacyDialedNumbers;
            model.TargetType = viewModel.TargetType;
            model.TargetAgentId = viewModel.TargetAgentId;
            model.TargetAgentOptions = viewModel.TargetAgentOptions;
            model.TargetQueueId = viewModel.TargetQueueId;
            model.TargetQueueOptions = viewModel.TargetQueueOptions;
            model.Priority = viewModel.Priority;
            model.BusinessHoursCalendarId = viewModel.BusinessHoursCalendarId;
            model.BusinessHoursCalendarOptions = viewModel.BusinessHoursCalendarOptions;
            model.ClosedAction = viewModel.ClosedAction;
            model.OverflowQueueId = viewModel.OverflowQueueId;
            model.OverflowQueueOptions = viewModel.OverflowQueueOptions;
            model.ClosedMessage = viewModel.ClosedMessage;
            model.Enabled = viewModel.Enabled;
        }

        return Combine(
            Initialize<EntryPointViewModel>("ContactCenterEntryPointGeneral_Edit", Populate).Location("Content:1%General;1"),
            Initialize<EntryPointViewModel>("ContactCenterEntryPointRouting_Edit", Populate).Location("Content:1%Routing;2"),
            Initialize<EntryPointViewModel>("ContactCenterEntryPointHours_Edit", Populate).Location("Content:1%Hours;3"));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ContactCenterEntryPoint entryPoint, UpdateEditorContext context)
    {
        var model = new EntryPointViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var isAgentTarget = model.TargetType == CrestApps.OrchardCore.ContactCenter.Models.EntryPointTargetType.Agent;

        entryPoint.Name = model.Name?.Trim();
        entryPoint.Description = model.Description?.Trim();
        entryPoint.Channel = entryPoint.GetChannel();

        // That each address exists and is used for the entry point's channel, and that no other enabled entry point
        // answers it on the same channel, is enforced by ContactCenterEntryPointHandler, so a recipe is held to it too.
        entryPoint.AddressIds = (model.AddressIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
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
        entryPoint.BusinessHoursCalendarId = string.IsNullOrWhiteSpace(model.BusinessHoursCalendarId) ? null : model.BusinessHoursCalendarId.Trim();
        entryPoint.ClosedAction = model.ClosedAction;
        entryPoint.OverflowQueueId = string.IsNullOrWhiteSpace(model.OverflowQueueId) ? null : model.OverflowQueueId.Trim();
        entryPoint.ClosedMessage = model.ClosedMessage?.Trim();
        entryPoint.Enabled = model.Enabled;

        return await EditAsync(entryPoint, context);
    }
}
