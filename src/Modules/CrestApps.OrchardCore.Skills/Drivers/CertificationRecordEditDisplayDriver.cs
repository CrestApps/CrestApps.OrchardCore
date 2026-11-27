using CrestApps.OrchardCore.Skills.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Skills.Drivers;

/// <summary>
/// The card of the editor of one person's certificate.
/// </summary>
internal sealed class CertificationRecordEditDisplayDriver : DisplayDriver<CertificationRecordEditViewModel>
{
    internal readonly IStringLocalizer S;

    public CertificationRecordEditDisplayDriver(IStringLocalizer<CertificationRecordEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(CertificationRecordEditViewModel model, BuildEditorContext context)
    {
        return Initialize<CertificationRecordEditViewModel>("CertificationRecordFields_Edit", m => Populate(model, m))
            .Location(l => l.Zone("Content", "1").Card(S["Certificate"], "10"));
    }

    public override async Task<IDisplayResult> UpdateAsync(CertificationRecordEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.UserId, m => m.CertificationId, m => m.Number, m => m.IssuedOn, m => m.ExpiresOn, m => m.DocumentPath, m => m.Status, m => m.Notes);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(CertificationRecordEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(CertificationRecordEditViewModel source, CertificationRecordEditViewModel model)
    {
        model.Id = source.Id;
        model.UserId = source.UserId;
        model.CertificationId = source.CertificationId;
        model.Number = source.Number;
        model.IssuedOn = source.IssuedOn;
        model.ExpiresOn = source.ExpiresOn;
        model.DocumentPath = source.DocumentPath;
        model.Status = source.Status;
        model.Notes = source.Notes;
        model.HeldByWorker = source.HeldByWorker;
        model.SubjectName = source.SubjectName;
        model.CertificationOptions = source.CertificationOptions;
        model.StatusOptions = source.StatusOptions;
    }
}
