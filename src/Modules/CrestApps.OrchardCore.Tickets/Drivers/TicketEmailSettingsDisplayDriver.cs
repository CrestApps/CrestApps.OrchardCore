using CrestApps.OrchardCore.Core.Emails;
using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tickets.Drivers;

/// <summary>
/// <b>Settings &gt; Tickets &gt; E-mails</b>: the site's own templates of the e-mails requesters get, through the shared
/// templated e-mail editor.
/// </summary>
internal sealed class TicketEmailSettingsDisplayDriver : SiteDisplayDriver<TicketEmailSettings>
{
    public const string GroupId = "ticket-emails";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly TemplatedEmailOptions _emailOptions;
    private readonly IStringLocalizerFactory _localizerFactory;

    internal readonly IStringLocalizer S;

    public TicketEmailSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IOptions<TemplatedEmailOptions> emailOptions,
        IStringLocalizerFactory localizerFactory,
        IStringLocalizer<TicketEmailSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _emailOptions = emailOptions.Value;
        _localizerFactory = localizerFactory;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => GroupId;

    public override IDisplayResult Edit(ISite site, TicketEmailSettings settings, BuildEditorContext context)
        => Initialize<TemplatedEmailTemplatesEditorViewModel>(TemplatedEmailTemplatesEditor.ShapeType, model =>
        {
            TemplatedEmailTemplatesEditor.Build(model, _emailOptions.GetKinds(TicketsConstants.EmailGroup), settings.Templates, settings.Layout, _localizerFactory);
            model.Introduction = S["Write the e-mails requesters get. In each, {{ Content }} is the e-mail as Tickets would send it and {{ Subject }} its subject; {{ Ticket.Number }}, {{ Ticket.Subject }}, {{ Ticket.Status }} and {{ Ticket.RequesterName }} describe the ticket. Dates show in the site's time zone. An e-mail whose template can't render is sent as built."];
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TicketsPermissions.ManageTicketSettings))
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, TicketEmailSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TicketsPermissions.ManageTicketSettings))
        {
            return null;
        }

        var model = new TemplatedEmailTemplatesEditorViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var edit = TemplatedEmailTemplatesEditor.Apply(model, _emailOptions.GetKinds(TicketsConstants.EmailGroup), settings.Templates);

        foreach (var (field, error) in edit.Errors)
        {
            context.Updater.ModelState.AddModelError(Prefix, field, S["The Liquid can't be used: {0}", error]);
        }

        settings.Layout = edit.Layout;
        settings.Templates = edit.Templates;

        return Edit(site, settings, context);
    }
}
