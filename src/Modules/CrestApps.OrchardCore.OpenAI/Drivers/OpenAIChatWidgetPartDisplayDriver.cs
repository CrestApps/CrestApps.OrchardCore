using CrestApps.OrchardCore.OpenAI.Core.Models;
using CrestApps.OrchardCore.OpenAI.Models;
using CrestApps.OrchardCore.OpenAI.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.OpenAI.Drivers;

public sealed class OpenAIChatWidgetPartDisplayDriver : ContentPartDisplayDriver<OpenAIChatWidgetPart>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOpenAIChatProfileStore _openAIChatProfileStore;
    private readonly IOpenAIChatSessionManager _chatSessionManager;
    private readonly PagerOptions _pagerOptions;

    internal readonly IStringLocalizer S;

    public OpenAIChatWidgetPartDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IOpenAIChatProfileStore openAIChatProfileStore,
        IOpenAIChatSessionManager chatSessionManager,
        IOptions<PagerOptions> pagerOptions,
        IStringLocalizer<OpenAIChatWidgetPartDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _openAIChatProfileStore = openAIChatProfileStore;
        _chatSessionManager = chatSessionManager;
        _pagerOptions = pagerOptions.Value;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> DisplayAsync(OpenAIChatWidgetPart part, BuildPartDisplayContext context)
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

        var profile = await _openAIChatProfileStore.FindByIdAsync(part.ProfileId);

        if (profile == null)
        {
            return null;
        }

        var sessions = await _chatSessionManager.PageAsync(1, part.TotalHistory.Value, new ChatSessionQueryContext
        {
            ProfileId = part.ProfileId,
        });

        if (sessions.Count == 0)
        {
            return null;
        }

        return Initialize<DisplayOpenAIChatWidgetViewModel>("OpenAIChatWidgetPart", model =>
        {
            model.Sessions = sessions.Sessions;
        }).Location("Detail", "History:5");
    }

    public override IDisplayResult Edit(OpenAIChatWidgetPart part, BuildPartEditorContext context)
    {
        return Initialize<OpenAIChatWidgetViewModel>("OpenAIChatWidgetPart_Edit", async model =>
        {
            model.ProfileId = part.ProfileId;

            model.TotalHistory = part.TotalHistory;

            model.MaxHistoryAllowed = _pagerOptions.MaxPageSize;

            var profiles = await _openAIChatProfileStore.GetAllAsync();
            model.Profiles = profiles.Where(x => x.Type == OpenAIChatProfileType.Chat)
            .Select(profile => new SelectListItem(profile.Name, profile.Id));

        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(OpenAIChatWidgetPart part, UpdatePartEditorContext context)
    {
        var model = new OpenAIChatWidgetViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(model.ProfileId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ProfileId), S["The Profile is required."]);
        }
        else if (await _openAIChatProfileStore.FindByIdAsync(model.ProfileId) == null)
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
