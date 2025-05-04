using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

public sealed class AIChatProfilePartDisplayDriver : ContentPartDisplayDriver<AIProfilePart>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly INamedModelStore<AIProfile> _profileStore;
    private readonly IAIChatSessionManager _chatSessionManager;
    private readonly PagerOptions _pagerOptions;

    internal readonly IStringLocalizer S;

    public AIChatProfilePartDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        INamedModelStore<AIProfile> profileStore,
        IAIChatSessionManager chatSessionManager,
        IOptions<PagerOptions> pagerOptions,
        IStringLocalizer<AIChatProfilePartDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _profileStore = profileStore;
        _chatSessionManager = chatSessionManager;
        _pagerOptions = pagerOptions.Value;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> DisplayAsync(AIProfilePart part, BuildPartDisplayContext context)
    {
        if (!part.TotalHistory.HasValue || part.TotalHistory.Value < 1)
        {
            return null;
        }

        var user = _httpContextAccessor?.HttpContext.User;

        // When displaying history, we should only target session that belong to authenticated users.
        if (user is null || !user.Identity.IsAuthenticated)
        {
            return null;
        }

        var profile = await _profileStore.FindByIdAsync(part.ProfileId);

        if (profile == null)
        {
            return null;
        }

        var sessions = await _chatSessionManager.PageAsync(1, part.TotalHistory.Value, new AIChatSessionQueryContext
        {
            ProfileId = part.ProfileId,
        });

        if (sessions.Count == 0)
        {
            return null;
        }

        return Initialize<DisplayAIChatWidgetViewModel>("AIChatWidgetPart", model =>
        {
            model.Sessions = sessions.Sessions;
        }).Location("Detail", "History:5");
    }

    public override IDisplayResult Edit(AIProfilePart part, BuildPartEditorContext context)
    {
        return Initialize<AIChatWidgetViewModel>("AIChatWidgetPart_Edit", async model =>
        {
            model.ProfileId = part.ProfileId;

            model.TotalHistory = part.TotalHistory;

            model.MaxHistoryAllowed = _pagerOptions.MaxPageSize;

            var profiles = await _profileStore.GetProfilesAsync(AIProfileType.Chat);

            model.Profiles = profiles.Select(profile => new SelectListItem(profile.DisplayText, profile.Id));

        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfilePart part, UpdatePartEditorContext context)
    {
        var model = new AIChatWidgetViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(model.ProfileId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ProfileId), S["The Profile is required."]);
        }
        else if (await _profileStore.FindByIdAsync(model.ProfileId) == null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ProfileId), S["The Profile is invalid."]);
        }

        if (model.TotalHistory.HasValue && (model.TotalHistory.Value < 0 || model.TotalHistory.Value > _pagerOptions.MaxPageSize))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TotalHistory), S["The Total History must be between {0} and {1}.", 0, _pagerOptions.MaxPageSize]);
        }

        part.TotalHistory = model.TotalHistory;
        part.ProfileId = model.ProfileId;

        return Edit(part, context);
    }
}
