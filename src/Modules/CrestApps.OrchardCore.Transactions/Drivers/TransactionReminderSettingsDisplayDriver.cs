using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Transactions.Drivers;

/// <summary>
/// Display driver that renders the transaction reminder settings tab.
/// </summary>
public sealed class TransactionReminderSettingsDisplayDriver : SiteDisplayDriver<TransactionReminderSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    protected override string SettingsGroupId
        => TransactionsConstants.SettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionReminderSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public TransactionReminderSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, TransactionReminderSettings settings, BuildEditorContext context)
    {
        return Initialize<TransactionReminderSettingsViewModel>("TransactionReminderSettings_Edit", model =>
        {
            model.Enabled = settings.Enabled;
            model.FirstReminderDelayDays = settings.FirstReminderDelayDays;
            model.ReminderIntervalDays = settings.ReminderIntervalDays;
            model.MaxReminders = settings.MaxReminders;
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TransactionsPermissions.ManageTransactionSettings))
        .OnGroup(SettingsGroupId);
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, TransactionReminderSettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, TransactionsPermissions.ManageTransactionSettings))
        {
            return null;
        }

        var model = new TransactionReminderSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.Enabled = model.Enabled;
        settings.FirstReminderDelayDays = Math.Max(0, model.FirstReminderDelayDays);
        settings.ReminderIntervalDays = Math.Max(1, model.ReminderIntervalDays);
        settings.MaxReminders = Math.Max(0, model.MaxReminders);

        return Edit(site, settings, context);
    }
}
