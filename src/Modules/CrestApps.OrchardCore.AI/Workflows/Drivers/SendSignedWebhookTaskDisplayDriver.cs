using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.AI.Workflows.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.AI.Workflows.Drivers;

/// <summary>
/// Edits the Send Signed Webhook task. The secret is kept encrypted and never shown again.
/// </summary>
public sealed class SendSignedWebhookTaskDisplayDriver : ActivityDisplayDriver<SendSignedWebhookTask, SendSignedWebhookTaskViewModel>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendSignedWebhookTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="liquidTemplateManager">The Liquid template manager, which validates the templates.</param>
    /// <param name="dataProtectionProvider">The data protection provider the secret is kept with.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SendSignedWebhookTaskDisplayDriver(
        ILiquidTemplateManager liquidTemplateManager,
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<SendSignedWebhookTaskDisplayDriver> stringLocalizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override void EditActivity(SendSignedWebhookTask activity, SendSignedWebhookTaskViewModel model)
    {
        model.Url = activity.Url;
        model.EventName = activity.EventName;
        model.Body = activity.Body;
        model.MaxAttempts = activity.MaxAttempts;
        model.HasSecret = !string.IsNullOrEmpty(activity.ProtectedSecret);
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(SendSignedWebhookTask activity, UpdateEditorContext context)
    {
        var model = new SendSignedWebhookTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Url))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Url), S["Enter the address to post to."]);
        }
        else if (!_liquidTemplateManager.Validate(model.Url, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Url), S["The URL is not a valid Liquid template."]);
        }

        if (!string.IsNullOrWhiteSpace(model.Body) && !_liquidTemplateManager.Validate(model.Body, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Body), S["The body is not a valid Liquid template."]);
        }

        if (string.IsNullOrWhiteSpace(model.Secret) && string.IsNullOrEmpty(activity.ProtectedSecret))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Secret), S["Enter the signing secret the receiver checks the signature with."]);
        }

        if (model.MaxAttempts is < 1 or > 5)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MaxAttempts), S["Attempts is from 1 to 5."]);
        }

        activity.Url = model.Url?.Trim();
        activity.EventName = model.EventName?.Trim();
        activity.Body = model.Body;
        activity.MaxAttempts = model.MaxAttempts;

        if (!string.IsNullOrWhiteSpace(model.Secret))
        {
            activity.ProtectedSecret = _dataProtectionProvider.CreateProtector(SendSignedWebhookTask.ProtectorPurpose).Protect(model.Secret.Trim());
        }

        return Edit(activity, context);
    }
}
