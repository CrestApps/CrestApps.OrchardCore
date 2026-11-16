using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tickets.AI;

/// <summary>
/// The AI assistant of a desk, on the desk's editor: the chat AI profile and whether a triage sets the priority.
/// </summary>
[RequireFeatures(AIConstants.Feature.Area)]
internal sealed class TicketDeskAIDisplayDriver : DisplayDriver<TicketDesk>
{
    private readonly IAIProfileManager _profiles;
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    public TicketDeskAIDisplayDriver(IAIProfileManager profiles, ISiteService siteService, IStringLocalizer<TicketDeskAIDisplayDriver> stringLocalizer)
    {
        _profiles = profiles;
        _siteService = siteService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(TicketDesk desk, BuildEditorContext context)
        => Initialize<TicketDeskAIViewModel>("TicketDeskAI_Edit", async model =>
        {
            var settings = desk.TryGet<TicketDeskAISettings>(out var current) ? current : new TicketDeskAISettings();

            var profiles = await _profiles.GetAsync(AIProfileType.Chat);
            var siteProfileId = (await _siteService.GetSiteSettingsAsync()).GetOrCreate<TicketAISettings>().ReplyProfileId;

            model.ProfileId = settings.Disabled ? TicketDeskAIViewModel.Off : settings.ProfileId;
            model.ApplyPriority = settings.ApplyPriority;
            model.SiteProfileName = profiles.FirstOrDefault(profile => profile.ItemId == siteProfileId) is { } site ? site.DisplayText ?? site.Name : null;
            model.Profiles = [.. profiles
                .OrderBy(profile => profile.DisplayText ?? profile.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(profile => new SelectListItem(profile.DisplayText ?? profile.Name, profile.ItemId, profile.ItemId == settings.ProfileId && !settings.Disabled))];
        }).Location("Content:6");

    public override async Task<IDisplayResult> UpdateAsync(TicketDesk desk, UpdateEditorContext context)
    {
        var model = new TicketDeskAIViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var disabled = string.Equals(model.ProfileId, TicketDeskAIViewModel.Off, StringComparison.Ordinal);
        var profileId = disabled || string.IsNullOrWhiteSpace(model.ProfileId) ? null : model.ProfileId;

        if (profileId is not null && await _profiles.FindByIdAsync(profileId) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.ProfileId), S["Choose an AI profile that exists."]);
        }
        else
        {
            desk.Put(new TicketDeskAISettings { ProfileId = profileId, Disabled = disabled, ApplyPriority = !disabled && model.ApplyPriority });
        }

        return Edit(desk, context);
    }
}
