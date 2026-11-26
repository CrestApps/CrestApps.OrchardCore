using System.Globalization;
using CrestApps.OrchardCore.Core.Emails;
using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Navigation;
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
/// The notification template cards of <b>Settings &gt; Service Desk</b>: the layout and the site's own template of
/// each message requesters get (a card each), through the shared templated e-mail editor.
/// </summary>
internal sealed class TicketEmailSettingsDisplayDriver : SiteDisplayDriver<TicketEmailSettings>
{
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
        => TicketSettingsAdminMenu.GroupId;

    public override IDisplayResult Edit(ISite site, TicketEmailSettings settings, BuildEditorContext context)
    {
        var kinds = _emailOptions.GetKinds(TicketsConstants.EmailGroup).ToList();

        void Fill(TemplatedEmailTemplatesEditorViewModel model, int index)
        {
            TemplatedEmailTemplatesEditor.Build(model, kinds, settings.Templates, settings.Layout, _localizerFactory);
            model.Index = index;
        }

        // The layout is one card and each kind of e-mail its own, so the templates are not frames inside a card.
        var results = new List<IDisplayResult>
        {
            Initialize<TemplatedEmailTemplatesEditorViewModel>(TemplatedEmailTemplatesEditor.LayoutShapeType, model =>
            {
                Fill(model, 0);
                model.Introduction = S["Write the messages requesters get. A requester who has a site account gets them through the site's notifications (in their notification center, and by e-mail or text message as they choose); anyone else gets them by e-mail. In each, {{ Content }} is the message as Tickets would send it and {{ Subject }} its subject; {{ Ticket.Number }}, {{ Ticket.Subject }}, {{ Ticket.Status }}, {{ Ticket.RequesterName }} and {{ Ticket.Category }} describe the ticket. Dates show in the site's time zone. A message whose template can't render is sent as built."];
            }).Location(l => l.Zone("Content", "5").Card(S["Notification Layout"], "50"))
            .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TicketsPermissions.ManageTicketSettings))
            .OnGroup(SettingsGroupId),
        };

        for (var i = 0; i < kinds.Count; i++)
        {
            var index = i;
            var title = _localizerFactory.Localize(kinds[i].Title).Value;

            results.Add(Initialize<TemplatedEmailTemplatesEditorViewModel>(TemplatedEmailTemplatesEditor.TemplateShapeType, model => Fill(model, index))
                .Differentiator(kinds[i].Name)
                .Location(l => l.Zone("Content", "5").Card(title, (51 + index).ToString(CultureInfo.InvariantCulture)))
                .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TicketsPermissions.ManageTicketSettings))
                .OnGroup(SettingsGroupId));
        }

        return Combine(results.ToArray());
    }

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
