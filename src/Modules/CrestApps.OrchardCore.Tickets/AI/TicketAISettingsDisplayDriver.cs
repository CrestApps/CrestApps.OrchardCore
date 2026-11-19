using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Tickets.Navigation;
using CrestApps.OrchardCore.Tickets.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tickets.AI;

/// <summary>
/// The AI card of Settings &gt; Service Desk: the profile that writes suggested replies and triages tickets on every desk
/// that names none. Registered only with AI on.
/// </summary>
[RequireFeatures(AIConstants.Feature.Area)]
internal sealed class TicketAISettingsDisplayDriver : SiteDisplayDriver<TicketAISettings>
{
    private readonly IAIProfileManager _profiles;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public TicketAISettingsDisplayDriver(
        IAIProfileManager profiles,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<TicketAISettingsDisplayDriver> stringLocalizer)
    {
        _profiles = profiles;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    // Settings > Service Desk, whose menu item and permission come with Tickets.
    protected override string SettingsGroupId
        => TicketSettingsAdminMenu.GroupId;

    public override IDisplayResult Edit(ISite site, TicketAISettings settings, BuildEditorContext context)
        => Initialize<TicketAISettingsViewModel>("TicketAISettings_Edit", async model =>
        {
            model.ReplyProfileId = settings.ReplyProfileId;
            model.Profiles = [.. (await _profiles.GetAsync(AIProfileType.Chat))
                .OrderBy(profile => profile.DisplayText ?? profile.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(profile => new SelectListItem(profile.DisplayText ?? profile.Name, profile.ItemId, profile.ItemId == settings.ReplyProfileId))];
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TicketsPermissions.ManageTicketSettings))
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, TicketAISettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TicketsPermissions.ManageTicketSettings))
        {
            return null;
        }

        var model = new TicketAISettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var profileId = string.IsNullOrWhiteSpace(model.ReplyProfileId) ? null : model.ReplyProfileId;

        if (profileId is not null && await _profiles.FindByIdAsync(profileId) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.ReplyProfileId), S["Choose an AI profile that exists."]);
        }
        else
        {
            settings.ReplyProfileId = profileId;
        }

        return Edit(site, settings, context);
    }
}
