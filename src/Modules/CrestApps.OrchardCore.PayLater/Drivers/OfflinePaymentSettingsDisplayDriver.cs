using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.PayLater.Models;
using CrestApps.OrchardCore.PayLater.ViewModels;
using CrestApps.OrchardCore.Transactions.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.PayLater.Drivers;

/// <summary>
/// The Bank transfer section of Settings › Commerce › Offline payments.
/// </summary>
public sealed class BankTransferSettingsDisplayDriver : SiteDisplayDriver<BankTransferSettings>
{
    private const int MaxInstructionsLength = 2000;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public BankTransferSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<BankTransferSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override string SettingsGroupId
        => PayLaterConstants.SettingsGroupId;

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, BankTransferSettings settings, BuildEditorContext context)
        => Initialize<BankTransferSettingsViewModel>("BankTransferSettings_Edit", model =>
        {
            model.Enabled = settings.Enabled;
            model.Instructions = settings.Instructions;
            model.NetTermDays = settings.NetTermDays;
        }).Location("Content:5")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TransactionsPermissions.ManageTransactionSettings))
        .OnGroup(SettingsGroupId);

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, BankTransferSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TransactionsPermissions.ManageTransactionSettings))
        {
            return null;
        }

        var model = new BankTransferSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var instructions = string.IsNullOrWhiteSpace(model.Instructions) ? null : model.Instructions.Trim();

        // Offering a transfer without saying where to send it leaves the buyer with nothing to do.
        if (model.Enabled && instructions is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Instructions), S["Add your bank details, so buyers know where to send the money."]);
        }
        else if (instructions?.Length > MaxInstructionsLength)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Instructions), S["Keep the instructions under {0} characters.", MaxInstructionsLength]);
        }
        else
        {
            settings.Enabled = model.Enabled;
            settings.Instructions = instructions;
            settings.NetTermDays = Math.Clamp(model.NetTermDays, 0, 365);
        }

        return Edit(site, settings, context);
    }
}

/// <summary>
/// The Cash on delivery section of Settings › Commerce › Offline payments.
/// </summary>
public sealed class CashOnDeliverySettingsDisplayDriver : SiteDisplayDriver<CashOnDeliverySettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public CashOnDeliverySettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc/>
    protected override string SettingsGroupId
        => PayLaterConstants.SettingsGroupId;

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, CashOnDeliverySettings settings, BuildEditorContext context)
        => Initialize<CashOnDeliverySettingsViewModel>("CashOnDeliverySettings_Edit", model =>
        {
            model.Enabled = settings.Enabled;
            model.Instructions = settings.Instructions;
        }).Location("Content:10")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TransactionsPermissions.ManageTransactionSettings))
        .OnGroup(SettingsGroupId);

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, CashOnDeliverySettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TransactionsPermissions.ManageTransactionSettings))
        {
            return null;
        }

        var model = new CashOnDeliverySettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.Enabled = model.Enabled;
        settings.Instructions = string.IsNullOrWhiteSpace(model.Instructions) ? null : model.Instructions.Trim()[..Math.Min(model.Instructions.Trim().Length, 1000)];

        return Edit(site, settings, context);
    }
}
