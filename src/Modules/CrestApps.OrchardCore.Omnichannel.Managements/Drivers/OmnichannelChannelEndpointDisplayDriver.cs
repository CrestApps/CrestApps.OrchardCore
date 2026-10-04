using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelChannelEndpointDisplayDriver : DisplayDriver<OmnichannelChannelEndpoint>
{
    private readonly OmnichannelAddressOptions _addressOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelChannelEndpointDisplayDriver"/> class.
    /// </summary>
    /// <param name="addressOptions">The address types and capabilities the enabled features registered.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelChannelEndpointDisplayDriver(
        IOptions<OmnichannelAddressOptions> addressOptions,
        IStringLocalizer<OmnichannelChannelEndpointDisplayDriver> stringLocalizer)
    {
        _addressOptions = addressOptions.Value;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(OmnichannelChannelEndpoint endpoint, BuildDisplayContext context)
    {
        return CombineAsync(
            View("OmnichannelChannelEndpoint_Fields_SummaryAdmin", endpoint)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
            View("OmnichannelChannelEndpoint_Buttons_SummaryAdmin", endpoint)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5"),
            View("OmnichannelChannelEndpoint_DefaultMeta_SummaryAdmin", endpoint)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5"),
            View("OmnichannelChannelEndpoint_CloneActionsMenu_SummaryAdmin", endpoint)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "ActionsMenu:5")
        );
    }

    public override IDisplayResult Edit(OmnichannelChannelEndpoint endpoint, BuildEditorContext context)
    {
        // The address type is fixed at creation, so it is shown, not edited. The settings of each capability are
        // contributed by the display drivers of the features that offer it, and are shown while it is ticked.
        return Initialize<OmnichannelChannelEndpointViewModel>("OmnichannelChannelEndpointFields_Edit", model =>
        {
            var addressType = endpoint.GetAddressType();
            var capabilities = endpoint.GetCapabilities();

            model.DisplayText = endpoint.DisplayText;
            model.Description = endpoint.Description;
            model.AddressType = addressType;
            model.AddressTypeDisplayName = _addressOptions.AddressTypes.TryGetValue(addressType ?? string.Empty, out var type) && type.DisplayName is not null
                ? type.DisplayName.Value
                : addressType;
            model.Value = endpoint.Value;
            model.Capabilities = [.. capabilities];
            model.AvailableCapabilities = _addressOptions.GetCapabilities(addressType)
                .Select(capability => new OmnichannelAddressCapabilityViewModel
                {
                    Name = capability.Name,
                    DisplayName = capability.DisplayName?.Value ?? capability.Name,
                    Description = capability.Description?.Value,
                    Selected = capabilities.Contains(capability.Name, StringComparer.OrdinalIgnoreCase),
                })
                .ToList();
        }).Location("Content:1%General;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelChannelEndpoint endpoint, UpdateEditorContext context)
    {
        var model = new OmnichannelChannelEndpointViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        endpoint.DisplayText = model.DisplayText?.Trim();
        endpoint.Description = model.Description?.Trim();
        endpoint.Value = model.Value?.Trim();
        endpoint.AddressType = endpoint.GetAddressType();
        endpoint.Capabilities = MergeCapabilities(endpoint, model.Capabilities);

        return Edit(endpoint, context);
    }

    // The form offers the capabilities of the enabled features. One whose feature is off is not on the form, and is
    // kept as it was rather than dropped, so switching a feature off and on again does not lose what the address does.
    private List<string> MergeCapabilities(OmnichannelChannelEndpoint endpoint, IEnumerable<string> posted)
    {
        var offered = _addressOptions.GetCapabilities(endpoint.GetAddressType())
            .Select(capability => capability.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var ticked = (posted ?? [])
            .Where(capability => !string.IsNullOrWhiteSpace(capability))
            .Select(capability => capability.Trim())
            .Where(offered.Contains)
            .Select(capability => _addressOptions.Capabilities[capability].Name);

        var kept = endpoint.GetCapabilities().Where(capability => !offered.Contains(capability));

        return ticked.Concat(kept)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
