using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Workflows;

/// <summary>
/// Edits the Send Text Message workflow task.
/// </summary>
public sealed class SendTextMessageTaskDisplayDriver : ActivityDisplayDriver<SendTextMessageTask, SendTextMessageTaskViewModel>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendTextMessageTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="liquidTemplateManager">The Liquid template manager, to validate the expressions.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SendTextMessageTaskDisplayDriver(
        ILiquidTemplateManager liquidTemplateManager,
        IStringLocalizer<SendTextMessageTaskDisplayDriver> stringLocalizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override void EditActivity(SendTextMessageTask activity, SendTextMessageTaskViewModel model)
    {
        model.From = activity.From;
        model.To = activity.To;
        model.Body = activity.Body;
        model.UserName = activity.UserName;
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(SendTextMessageTask activity, UpdateEditorContext context)
    {
        var model = new SendTextMessageTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        Require(model.From, nameof(model.From), S["The From number is required."], context);
        Require(model.To, nameof(model.To), S["The To number is required."], context);
        Require(model.Body, nameof(model.Body), S["The message is required."], context);

        if (!string.IsNullOrWhiteSpace(model.UserName) && !_liquidTemplateManager.Validate(model.UserName, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.UserName), S["The Agent expression is invalid."]);
        }

        activity.From = model.From?.Trim();
        activity.To = model.To?.Trim();
        activity.Body = model.Body;
        activity.UserName = model.UserName?.Trim();

        return Edit(activity, context);
    }

    private void Require(string value, string member, LocalizedString message, UpdateEditorContext context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            context.Updater.ModelState.AddModelError(Prefix, member, message);
        }
        else if (!_liquidTemplateManager.Validate(value, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, member, S["The expression is invalid."]);
        }
    }
}
