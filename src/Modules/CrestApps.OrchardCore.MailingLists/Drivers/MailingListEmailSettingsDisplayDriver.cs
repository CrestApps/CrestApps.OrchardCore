using CrestApps.OrchardCore.Core.Emails;
using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.MailingLists.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.MailingLists.Drivers;

/// <summary>
/// The E-mail templates card of <b>Settings &gt; Mailing Lists</b>: the site's own templates of the confirmation,
/// welcome and coupon claim e-mails, through the shared templated e-mail editor.
/// </summary>
internal sealed class MailingListEmailSettingsDisplayDriver : SiteDisplayDriver<MailingListEmailSettings>
{
    private const string CardWrapper = "MailingListEmailTemplatesCard_Wrapper";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly TemplatedEmailOptions _emailOptions;
    private readonly IStringLocalizerFactory _localizerFactory;

    internal readonly IStringLocalizer S;

    public MailingListEmailSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IOptions<TemplatedEmailOptions> emailOptions,
        IStringLocalizerFactory localizerFactory,
        IStringLocalizer<MailingListEmailSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _emailOptions = emailOptions.Value;
        _localizerFactory = localizerFactory;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => MailingListsConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, MailingListEmailSettings settings, BuildEditorContext context)
        => Initialize<TemplatedEmailTemplatesEditorViewModel>(TemplatedEmailTemplatesEditor.ShapeType, model =>
        {
            TemplatedEmailTemplatesEditor.Build(model, _emailOptions.GetKinds(MailingListsConstants.EmailGroup), settings.Templates, settings.Layout, _localizerFactory);
            model.Introduction = S["Write the e-mails people get when they sign up to a mailing list. In each, {{ Content }} is the e-mail as the site would send it and {{ Subject }} its subject; {{ List.Name }}, {{ List.Description }}, {{ Member.Name }}, {{ Member.Email }}, {{ Links.Verify }} and {{ Links.Unsubscribe }} describe the sign-up, and {{ Coupon.Code }}, {{ Coupon.Url }}, {{ Coupon.Description }} and {{ Coupon.Expires }} the welcome coupon when there is one. Dates show in the site's time zone. Keep the unsubscribe link in every template. An e-mail whose template can't render is sent as built."];
        }).Location("Content:5")
        .Displaying(context => context.Shape.Metadata.Wrappers.Add(CardWrapper))
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, MailingListsPermissions.ManageMailingListSettings))
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, MailingListEmailSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, MailingListsPermissions.ManageMailingListSettings))
        {
            return null;
        }

        var model = new TemplatedEmailTemplatesEditorViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var edit = TemplatedEmailTemplatesEditor.Apply(model, _emailOptions.GetKinds(MailingListsConstants.EmailGroup), settings.Templates);

        foreach (var (field, error) in edit.Errors)
        {
            context.Updater.ModelState.AddModelError(Prefix, field, S["The Liquid can't be used: {0}", error]);
        }

        settings.Layout = edit.Layout;
        settings.Templates = edit.Templates;

        return Edit(site, settings, context);
    }
}
