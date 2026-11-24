using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Drivers;

/// <summary>
/// The display-management driver for <see cref="MessageTemplate"/>: the admin list row and the create/edit form.
/// </summary>
public sealed class MessageTemplateDisplayDriver : DisplayDriver<MessageTemplate>
{
    private readonly IOptions<MessageTemplateScopeOptions> _scopeOptions;
    private readonly IStringLocalizerFactory _localizerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageTemplateDisplayDriver"/> class.
    /// </summary>
    /// <param name="scopeOptions">The scopes other modules registered.</param>
    /// <param name="localizerFactory">The localizer factory, for the scopes' titles.</param>
    public MessageTemplateDisplayDriver(IOptions<MessageTemplateScopeOptions> scopeOptions, IStringLocalizerFactory localizerFactory)
    {
        _scopeOptions = scopeOptions;
        _localizerFactory = localizerFactory;
    }

    public override IDisplayResult Display(MessageTemplate template, BuildDisplayContext context)
    {
        // A template is only displayed as a row of the admin list of templates.
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = template.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return Combine(
            View("MessageTemplate_Checkbox_SummaryAdmin", template)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Checkbox:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Select:1"),
            View("MessageTemplate_Fields_SummaryAdmin", template)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("MessageTemplate_Description_SummaryAdmin", template)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(template.Body))),
            View("MessageTemplate_Buttons_SummaryAdmin", template)
                .Location("Actions:5"));
    }

    public override IDisplayResult Edit(MessageTemplate template, BuildEditorContext context)
    {
        return Initialize<MessageTemplateViewModel>("MessageTemplateFields_Edit", model =>
        {
            model.Name = template.Name;
            model.Body = template.Body;
            model.Scope = template.Scope;
            // A new template fills in its placeholders; an existing one keeps what it was saved with.
            model.FillsPlaceholders = context.IsNew || template.FillsPlaceholders;
            model.Scopes = [.. _scopeOptions.Value.Scopes.Select(scope => new KeyValuePair<string, string>(scope.Key, _localizerFactory.Localize(scope.Value).Value))];

            // A scope this site doesn't register (from another site's recipe) is shown as it is, so saving keeps it.
            if (!string.IsNullOrEmpty(template.Scope) && !_scopeOptions.Value.Scopes.ContainsKey(template.Scope))
            {
                model.Scopes.Add(new KeyValuePair<string, string>(template.Scope, template.Scope));
            }
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(MessageTemplate template, UpdateEditorContext context)
    {
        var model = new MessageTemplateViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        template.Name = model.Name?.Trim();
        template.Body = model.Body?.Trim();
        template.FillsPlaceholders = model.FillsPlaceholders;

        // A registered scope, or the one the template already had; anything else is the messaging workspace's.
        template.Scope = !string.IsNullOrEmpty(model.Scope)
            && (_scopeOptions.Value.Scopes.ContainsKey(model.Scope) || string.Equals(model.Scope, template.Scope, StringComparison.Ordinal))
                ? model.Scope
                : null;

        return Edit(template, context);
    }
}
