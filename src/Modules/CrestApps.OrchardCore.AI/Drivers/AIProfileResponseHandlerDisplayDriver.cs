using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.ResponseHandling;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Display driver for the <see cref="ResponseHandlerProfileSettings"/> section on AI profiles.
/// Shows a dropdown to select the initial response handler when non-AI handlers are registered.
/// </summary>
internal sealed class AIProfileResponseHandlerDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IChatResponseHandlerResolver _handlerResolver;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileResponseHandlerDisplayDriver"/> class.
    /// </summary>
    /// <param name="handlerResolver">The handler resolver.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileResponseHandlerDisplayDriver(
        IChatResponseHandlerResolver handlerResolver,
        IStringLocalizer<AIProfileResponseHandlerDisplayDriver> stringLocalizer)
    {
        _handlerResolver = handlerResolver;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        var handlers = _handlerResolver.GetAll();

        // Only show the handler selector when there is at least one non-AI handler registered.

        if (!handlers.Any())
        {
            return null;
        }

        return Initialize<EditResponseHandlerProfileSettingsViewModel>("AIProfileResponseHandler_Edit", model =>
        {
            var settings = profile.GetSettings<ResponseHandlerProfileSettings>();

            model.InitialResponseHandlerName = settings.InitialResponseHandlerName;

            model.ResponseHandlers = handlers
            .Select(h => new SelectListItem(h.Name, h.Name))
            .OrderBy(x => x.Text)
            .ToList();
        }).Location("Content:9%General;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var handlers = _handlerResolver.GetAll().ToList();

        if (handlers.Count <= 1)
        {
            return null;
        }

        var model = new EditResponseHandlerProfileSettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        profile.AlterSettings<ResponseHandlerProfileSettings>(settings =>
        {
            settings.InitialResponseHandlerName = model.InitialResponseHandlerName?.Trim();
        });

        return Edit(profile, context);
    }
}
