using System.Globalization;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Skills.Drivers;

/// <summary>
/// The main card of the certification editor. Other features and modules add their own cards with display drivers
/// for <see cref="Certification"/>.
/// </summary>
internal sealed class CertificationDisplayDriver : DisplayDriver<Certification>
{
    internal readonly IStringLocalizer S;

    public CertificationDisplayDriver(IStringLocalizer<CertificationDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(Certification certification, BuildEditorContext context)
    {
        return Initialize<CertificationEditViewModel>("CertificationFields_Edit", model =>
        {
            model.Name = certification.Name;
            model.Issuer = certification.Issuer;
            model.Description = certification.Description;
            model.ValidityMonths = certification.ValidityMonths;
            model.RequiresDocument = certification.RequiresDocument;
            model.ReminderDays = string.Join(", ", certification.ReminderDays);
            model.Enabled = certification.Enabled;
        }).Location(l => l.Zone("Content", "1").Card(S["Certification"], "10"));
    }

    public override async Task<IDisplayResult> UpdateAsync(Certification certification, UpdateEditorContext context)
    {
        var model = new CertificationEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var reminderDays = new List<int>();

        foreach (var part in (model.ReminderDays ?? string.Empty).Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) && days > 0)
            {
                reminderDays.Add(days);
            }
            else
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ReminderDays), S["'{0}' is not a number of days.", part]);
            }
        }

        certification.Name = model.Name;
        certification.Issuer = model.Issuer;
        certification.Description = model.Description;
        certification.ValidityMonths = model.ValidityMonths;
        certification.RequiresDocument = model.RequiresDocument;
        certification.ReminderDays = reminderDays;
        certification.Enabled = model.Enabled;

        return Edit(certification, context);
    }
}
