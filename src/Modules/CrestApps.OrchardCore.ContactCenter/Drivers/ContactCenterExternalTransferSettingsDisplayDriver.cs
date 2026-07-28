using System;
using System.Collections.Generic;
using System.Linq;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

/// <summary>
/// Display driver that renders and persists the tenant-scoped approved external transfer
/// destination catalog on the Contact Center site settings screen.
/// </summary>
public sealed class ContactCenterExternalTransferSettingsDisplayDriver
    : SiteDisplayDriver<ContactCenterExternalTransferSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    /// <inheritdoc/>
    protected override string SettingsGroupId
        => ContactCenterConstants.Settings.GroupId;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="ContactCenterExternalTransferSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterExternalTransferSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<ContactCenterExternalTransferSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(
        ISite site,
        ContactCenterExternalTransferSettings settings,
        BuildEditorContext context)
    {
        return Initialize<ContactCenterExternalTransferSettingsViewModel>(
            "ContactCenterExternalTransferSettings_Edit",
            model =>
            {
                model.Destinations = settings.Destinations
                    .Select(d => new ContactCenterExternalDestinationViewModel
                    {
                        Id = d.Id,
                        DisplayName = d.DisplayName,
                        E164Address = d.E164Address,
                        Enabled = d.Enabled,
                    })
                    .ToList();
            })
            .Location("Content:5#External transfer destinations")
            .OnGroup(SettingsGroupId)
            .RenderWhen(() => _authorizationService.AuthorizeAsync(
                _httpContextAccessor.HttpContext?.User,
                ContactCenterPermissions.ManageContactCenter));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(
        ISite site,
        ContactCenterExternalTransferSettings settings,
        UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(
                _httpContextAccessor.HttpContext?.User,
                ContactCenterPermissions.ManageContactCenter))
        {
            return null;
        }

        var model = new ContactCenterExternalTransferSettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var destinations = new List<ContactCenterExternalDestination>();

        for (var i = 0; i < model.Destinations.Count; i++)
        {
            var entry = model.Destinations[i];
            var address = entry.E164Address?.Trim();
            var displayName = entry.DisplayName?.Trim();

            if (string.IsNullOrWhiteSpace(displayName) && string.IsNullOrWhiteSpace(address))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                context.Updater.ModelState.AddModelError(
                    Prefix,
                    $"Destinations[{i}].DisplayName",
                    S["Enter a display name for destination {0}.", i + 1]);
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                context.Updater.ModelState.AddModelError(
                    Prefix,
                    $"Destinations[{i}].E164Address",
                    S["Enter an E.164 address for destination {0}.", i + 1]);
            }
            else if (!ExternalDestinationPolicy.IsAllowed(address))
            {
                context.Updater.ModelState.AddModelError(
                    Prefix,
                    $"Destinations[{i}].E164Address",
                    S["Enter a valid E.164 phone number starting with + for destination {0}. Emergency and premium-rate numbers are not permitted.", i + 1]);
            }

            var id = string.IsNullOrWhiteSpace(entry.Id)
                ? IdGenerator.GenerateId()
                : entry.Id.Trim();

            destinations.Add(new ContactCenterExternalDestination
            {
                Id = id,
                DisplayName = displayName ?? string.Empty,
                E164Address = address ?? string.Empty,
                Enabled = entry.Enabled,
            });
        }

        if (context.Updater.ModelState.IsValid)
        {
            settings.Destinations = destinations;
        }

        return Edit(site, settings, context);
    }
}
