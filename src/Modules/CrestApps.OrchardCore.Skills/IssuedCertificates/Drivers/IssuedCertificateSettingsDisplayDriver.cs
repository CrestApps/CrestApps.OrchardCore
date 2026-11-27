using CrestApps.OrchardCore.Skills.IssuedCertificates.ViewModels;
using CrestApps.OrchardCore.Skills.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Records;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using YesSql;

namespace CrestApps.OrchardCore.Skills.IssuedCertificates.Drivers;

/// <summary>
/// The "Issued certificates" card of the certification editor: generated numbers and the template.
/// </summary>
internal sealed class IssuedCertificateSettingsDisplayDriver : DisplayDriver<Certification>
{
    private readonly ISession _session;

    internal readonly IStringLocalizer S;

    public IssuedCertificateSettingsDisplayDriver(ISession session, IStringLocalizer<IssuedCertificateSettingsDisplayDriver> stringLocalizer)
    {
        _session = session;
        S = stringLocalizer;
    }

    // Every driver of a model gets the model's name as its prefix; this card has its own.
    protected override void BuildPrefix(Certification model, string htmlFieldPrefix)
        => Prefix = string.IsNullOrEmpty(htmlFieldPrefix) ? "IssuedCertificates" : $"{htmlFieldPrefix}.IssuedCertificates";

    public override IDisplayResult Edit(Certification certification, BuildEditorContext context)
    {
        return Initialize<IssuedCertificateSettingsViewModel>("IssuedCertificateSettings_Edit", async model =>
        {
            model.GenerateNumbers = certification.GenerateNumbers;
            model.NumberPrefix = certification.NumberPrefix;
            model.CertificateTemplateId = certification.CertificateTemplateId;
            model.CertificationId = context.IsNew ? null : certification.ItemId;

            var templates = await _session.QueryIndex<ContentItemIndex>(index => index.ContentType == SkillsConstants.CertificateTemplateContentType && index.Published)
                .OrderBy(index => index.DisplayText)
                .ListAsync();

            model.TemplateOptions = templates
                .Select(index => new SelectListItem(index.DisplayText, index.ContentItemId, index.ContentItemId == certification.CertificateTemplateId))
                .ToList();
        }).Location(l => l.Zone("Content", "5").Card(S["Issued Certificates"], "20"));
    }

    public override async Task<IDisplayResult> UpdateAsync(Certification certification, UpdateEditorContext context)
    {
        var model = new IssuedCertificateSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        certification.GenerateNumbers = model.GenerateNumbers;
        certification.NumberPrefix = string.IsNullOrWhiteSpace(model.NumberPrefix) ? null : model.NumberPrefix.Trim();
        certification.CertificateTemplateId = string.IsNullOrWhiteSpace(model.CertificateTemplateId) ? null : model.CertificateTemplateId;

        return Edit(certification, context);
    }
}
