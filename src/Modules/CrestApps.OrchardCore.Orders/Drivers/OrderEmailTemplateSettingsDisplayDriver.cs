using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Drivers;

/// <summary>
/// Settings › Commerce › Emails: the store's own templates of its emails about orders, and a layout around them.
/// </summary>
public sealed class OrderEmailTemplateSettingsDisplayDriver : SiteDisplayDriver<OrderEmailTemplateSettings>
{
    public const string GroupId = "order-emails";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILiquidTemplateManager _liquid;

    internal readonly IStringLocalizer S;

    public OrderEmailTemplateSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        ILiquidTemplateManager liquid,
        IStringLocalizer<OrderEmailTemplateSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _liquid = liquid;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => GroupId;

    public override IDisplayResult Edit(ISite site, OrderEmailTemplateSettings settings, BuildEditorContext context)
        => Initialize<OrderEmailTemplateSettingsViewModel>("OrderEmailTemplateSettings_Edit", model =>
        {
            model.Layout = settings.Layout;
            model.Templates = [.. OrderEmailKinds.All.Select(kind => settings.Templates.TryGetValue(kind, out var template)
                ? new OrderEmailTemplateViewModel { Kind = kind, IsEnabled = template.IsEnabled, Subject = template.Subject, Body = template.Body }
                : new OrderEmailTemplateViewModel { Kind = kind, Body = "{{ Content }}" })];
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, OrderPermissions.ManageOrders))
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, OrderEmailTemplateSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, OrderPermissions.ManageOrders))
        {
            return null;
        }

        var model = new OrderEmailTemplateSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // A template that doesn't parse is refused, so a typo is found here and not when an order is paid.
        foreach (var (field, liquid) in (model.Templates ?? []).SelectMany((template, index) => new[]
        {
            ($"Templates[{index}].Subject", template.Subject),
            ($"Templates[{index}].Body", template.Body),
        }).Append((nameof(model.Layout), model.Layout)))
        {
            if (!string.IsNullOrWhiteSpace(liquid) && !_liquid.Validate(liquid, out var errors))
            {
                context.Updater.ModelState.AddModelError(Prefix, field, S["The Liquid doesn't parse: {0}", string.Join(" ", errors)]);
            }
        }

        settings.Layout = string.IsNullOrWhiteSpace(model.Layout) ? null : model.Layout;
        settings.Templates = (model.Templates ?? [])
            .Where(template => OrderEmailKinds.All.Contains(template.Kind))
            .ToDictionary(template => template.Kind, template => new OrderEmailTemplate
            {
                IsEnabled = template.IsEnabled,
                Subject = string.IsNullOrWhiteSpace(template.Subject) ? null : template.Subject.Trim(),
                Body = string.IsNullOrWhiteSpace(template.Body) ? null : template.Body,
            }, StringComparer.Ordinal);

        return Edit(site, settings, context);
    }
}

public class OrderEmailTemplateSettingsViewModel
{
    public string Layout { get; set; }

    public List<OrderEmailTemplateViewModel> Templates { get; set; } = [];
}

public class OrderEmailTemplateViewModel
{
    public string Kind { get; set; }

    public bool IsEnabled { get; set; }

    public string Subject { get; set; }

    public string Body { get; set; }
}
