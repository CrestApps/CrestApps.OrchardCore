using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

internal sealed class AgentStateReasonCodeDisplayDriver : DisplayDriver<AgentStateReasonCode>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStateReasonCodeDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AgentStateReasonCodeDisplayDriver(IStringLocalizer<AgentStateReasonCodeDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(AgentStateReasonCode reasonCode, BuildDisplayContext context)
    {
        return CombineAsync(
            View("AgentStateReasonCode_Fields_SummaryAdmin", reasonCode)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
            View("AgentStateReasonCode_Buttons_SummaryAdmin", reasonCode)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5"),
            View("AgentStateReasonCode_DefaultMeta_SummaryAdmin", reasonCode)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
        );
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(AgentStateReasonCode reasonCode, BuildEditorContext context)
    {
        return Initialize<AgentStateReasonCodeViewModel>("AgentStateReasonCodeFields_Edit", model =>
        {
            model.Id = reasonCode.ItemId;
            model.Name = reasonCode.Name;
            model.Description = reasonCode.Description;
            model.AppliesTo = reasonCode.AppliesTo;
            model.SortOrder = reasonCode.SortOrder;
            model.Enabled = reasonCode.Enabled;
            model.AppliesToOptions = GetAppliesToOptions(reasonCode.AppliesTo);
        }).Location("Content:1");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(AgentStateReasonCode reasonCode, UpdateEditorContext context)
    {
        var model = new AgentStateReasonCodeViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Name is a required field."]);
        }

        reasonCode.Name = model.Name?.Trim();
        reasonCode.Description = model.Description?.Trim();
        reasonCode.AppliesTo = model.AppliesTo;
        reasonCode.SortOrder = model.SortOrder;
        reasonCode.Enabled = model.Enabled;

        return Edit(reasonCode, context);
    }

    private List<SelectListItem> GetAppliesToOptions(AgentPresenceStatus selected)
    {
        return
        [
            CreateOption(S["Break"], AgentPresenceStatus.Break, selected),
            CreateOption(S["Away"], AgentPresenceStatus.Away, selected),
            CreateOption(S["Do not disturb"], AgentPresenceStatus.DoNotDisturb, selected),
            CreateOption(S["Meeting"], AgentPresenceStatus.Meeting, selected),
            CreateOption(S["Training"], AgentPresenceStatus.Training, selected),
            CreateOption(S["After-hours unavailable"], AgentPresenceStatus.AfterHoursUnavailable, selected),
        ];
    }

    private static SelectListItem CreateOption(LocalizedString text, AgentPresenceStatus status, AgentPresenceStatus selected)
    {
        return new SelectListItem(text.Value, status.ToString(), status == selected);
    }
}
