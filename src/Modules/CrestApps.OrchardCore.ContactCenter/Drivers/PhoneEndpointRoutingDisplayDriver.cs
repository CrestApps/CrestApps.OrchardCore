using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

/// <summary>
/// Adds inbound routing to a phone number's editor: the entry point that answers calls to it, read by
/// <see cref="ChannelEndpointEntryPointResolver"/>. An SMS number is routed from its own screen, and a phone number is
/// now too, instead of only by typing it into an entry point. That the chosen entry point exists is enforced by
/// <see cref="Services.PhoneEndpointRoutingRule"/>, so a recipe is held to it too.
/// </summary>
public sealed class PhoneEndpointRoutingDisplayDriver : DisplayDriver<OmnichannelChannelEndpoint>
{
    private readonly IContactCenterEntryPointManager _entryPointManager;

    internal readonly IStringLocalizer S;

    public PhoneEndpointRoutingDisplayDriver(
        IContactCenterEntryPointManager entryPointManager,
        IStringLocalizer<PhoneEndpointRoutingDisplayDriver> stringLocalizer)
    {
        _entryPointManager = entryPointManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(OmnichannelChannelEndpoint endpoint, BuildEditorContext context)
    {
        if (!IsPhoneEndpoint(endpoint))
        {
            return null;
        }

        return Initialize<PhoneEndpointRoutingViewModel>("PhoneEndpointRouting_Edit", async model =>
        {
            var selectedId = endpoint.GetOrCreate<PhoneEndpointRoutingSettings>().EntryPointId;
            var entryPoints = await _entryPointManager.GetAllAsync();

            model.EntryPointId = selectedId;
            model.EntryPoints = entryPoints
                .OrderBy(entryPoint => entryPoint.Name, StringComparer.OrdinalIgnoreCase)
                .Select(entryPoint => new SelectListItem(
                    entryPoint.Enabled ? entryPoint.Name : S["{0} (disabled)", entryPoint.Name].Value,
                    entryPoint.ItemId,
                    string.Equals(entryPoint.ItemId, selectedId, StringComparison.Ordinal)))
                .ToList();

            // The entry points that already list the number keep answering it when none is chosen here, so the
            // editor says which they are rather than leaving the admin to wonder why calls still arrive.
            if (!string.IsNullOrWhiteSpace(endpoint.Value))
            {
                model.ListedOnEntryPoints = entryPoints
                    .Where(entryPoint => entryPoint.DialedNumbers is not null &&
                        entryPoint.DialedNumbers.Any(number => string.Equals(number?.Trim(), endpoint.Value, StringComparison.OrdinalIgnoreCase)))
                    .Select(entryPoint => entryPoint.Name)
                    .ToList();
            }
        }).Location("Content:1%Voice calls;2");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(OmnichannelChannelEndpoint endpoint, UpdateEditorContext context)
    {
        if (!IsPhoneEndpoint(endpoint))
        {
            return Edit(endpoint, context);
        }

        var model = new PhoneEndpointRoutingViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // That the entry point exists is checked by PhoneEndpointRoutingRule, so a recipe is held to it too.
        endpoint.Put(new PhoneEndpointRoutingSettings
        {
            EntryPointId = string.IsNullOrWhiteSpace(model.EntryPointId) ? null : model.EntryPointId.Trim(),
        });

        return Edit(endpoint, context);
    }

    private static bool IsPhoneEndpoint(OmnichannelChannelEndpoint endpoint)
        => string.Equals(endpoint?.GetAddressType(), OmnichannelAddressTypes.PhoneNumber, StringComparison.OrdinalIgnoreCase);
}
