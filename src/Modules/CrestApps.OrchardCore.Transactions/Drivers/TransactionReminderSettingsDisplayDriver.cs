using System.Globalization;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Transactions.Drivers;

/// <summary>
/// Display driver that renders the transaction reminder settings tab.
/// </summary>
public sealed class TransactionReminderSettingsDisplayDriver : SiteDisplayDriver<TransactionReminderSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    protected override string SettingsGroupId
        => TransactionsConstants.SettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionReminderSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionReminderSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<TransactionReminderSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, TransactionReminderSettings settings, BuildEditorContext context)
    {
        ShapeResult Card(string shapeType, string position, LocalizedString card, string cardPosition)
            => Initialize<TransactionReminderSettingsViewModel>(shapeType, model =>
            {
                model.Enabled = settings.Enabled;
                model.FirstReminderDelayDays = settings.FirstReminderDelayDays;
                model.ReminderIntervalDays = settings.ReminderIntervalDays;
                model.MaxReminders = settings.MaxReminders;
                model.UpcomingReminderDays = settings.UpcomingReminderDays;
                model.Steps = string.Join(", ", (settings.Steps ?? []).Select(day => day.ToString(CultureInfo.InvariantCulture)));
                model.SendFromHour = settings.SendFromHour;
                model.SendUntilHour = settings.SendUntilHour;
                model.ChargeRetryDays = string.Join(", ", (settings.ChargeRetryDays ?? []).Select(day => day.ToString(CultureInfo.InvariantCulture)));
            }).Location(l => l.Zone("Content", position).Card(card, cardPosition))
            .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TransactionsPermissions.ManageTransactionSettings))
            .OnGroup(SettingsGroupId);

        return Combine(
            Card("TransactionReminderSettingsReminders_Edit", "1", S["Reminders"], "10"),
            Card("TransactionReminderSettingsHours_Edit", "1.1", S["Sending Hours"], "20"),
            Card("TransactionReminderSettingsRetries_Edit", "1.2", S["Card Retries"], "30"));
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
        settings.UpcomingReminderDays = Math.Max(0, model.UpcomingReminderDays);
        settings.SendFromHour = Math.Clamp(model.SendFromHour, 0, 23);
        settings.SendUntilHour = Math.Clamp(model.SendUntilHour, 1, 24);

        if (TryParseDays(model.Steps, allowNegative: true, out var steps))
        {
            settings.Steps = steps;
        }
        else
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Steps), S["Enter up to 12 whole numbers of days between -365 and 365, separated by commas, such as -3, 0, 7, 14."]);
        }

        if (TryParseDays(model.ChargeRetryDays, allowNegative: false, out var retries))
        {
            settings.ChargeRetryDays = retries;
        }
        else
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ChargeRetryDays), S["Enter up to 12 whole numbers of days between 1 and 365, separated by commas, such as 1, 3, 5."]);
        }

        return Edit(site, settings, context);
    }

    // A list of whole days separated by commas; empty means none.
    internal static bool TryParseDays(string value, bool allowNegative, out int[] days)
    {
        days = [];

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var parsed = new List<int>();

        foreach (var part in value.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var day) ||
                day > 365 ||
                day < (allowNegative ? -365 : 1))
            {
                return false;
            }

            parsed.Add(day);
        }

        if (parsed.Count > 12)
        {
            return false;
        }

        days = allowNegative ? [.. parsed.Distinct().Order()] : [.. parsed];

        return true;
    }
}
