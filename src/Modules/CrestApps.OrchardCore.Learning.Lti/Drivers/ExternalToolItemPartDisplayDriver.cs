using CrestApps.OrchardCore.Learning.Lti.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Learning.Lti.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Learning.Lti.Drivers;

/// <summary>
/// The External tool part in the content editor: the registered tool, the target link URI and custom parameters of this
/// link, and the maximum score of its line item.
/// </summary>
internal sealed class ExternalToolItemPartDisplayDriver : ContentPartDisplayDriver<ExternalToolItemPart>
{
    private readonly IExternalToolStore _tools;

    internal readonly IStringLocalizer S;

    public ExternalToolItemPartDisplayDriver(IExternalToolStore tools, IStringLocalizer<ExternalToolItemPartDisplayDriver> stringLocalizer)
    {
        _tools = tools;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ExternalToolItemPart part, BuildPartEditorContext context)
        => Initialize<ExternalToolItemPartViewModel>(GetEditorShapeType(context), async model =>
        {
            model.ToolId = part.ToolId;
            model.TargetLinkUri = part.TargetLinkUri;
            model.CustomParameters = part.CustomParameters;
            model.ScoreMaximum = part.ScoreMaximum;
            model.LineItemLabel = part.LineItemLabel;
            model.DeepLinked = !string.IsNullOrEmpty(part.DeepLinkedContent);
            model.Tools = (await _tools.ListAllAsync())
                .Select(tool => new SelectListItem(tool.Enabled ? tool.Name : S["{0} (turned off)", tool.Name], tool.ItemId, tool.ItemId == part.ToolId))
                .ToList();
        });

    public override async Task<IDisplayResult> UpdateAsync(ExternalToolItemPart part, UpdatePartEditorContext context)
    {
        var model = new ExternalToolItemPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(model.ToolId) || await _tools.FindByIdAsync(model.ToolId) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ToolId), S["Choose the external tool this item launches."]);
        }

        if (!string.IsNullOrWhiteSpace(model.TargetLinkUri) &&
            (!Uri.TryCreate(model.TargetLinkUri.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TargetLinkUri), S["Enter a full address that starts with https://, or leave it empty for the tool's default."]);
        }

        if (model.ScoreMaximum is <= 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ScoreMaximum), S["The maximum score must be more than 0."]);
        }

        part.ToolId = model.ToolId;
        part.TargetLinkUri = string.IsNullOrWhiteSpace(model.TargetLinkUri) ? null : model.TargetLinkUri.Trim();
        part.CustomParameters = model.CustomParameters?.Trim();
        part.ScoreMaximum = model.ScoreMaximum;
        part.LineItemLabel = string.IsNullOrWhiteSpace(model.LineItemLabel) ? null : model.LineItemLabel.Trim();

        return Edit(part, context);
    }
}
