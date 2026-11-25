using CrestApps.OrchardCore.Learning.Programs.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Programs.Drivers;

/// <summary>
/// The editor of a required learning rule: what is required, who is required, the due date and reminders, and whether
/// the rule is on.
/// </summary>
internal sealed class RequiredLearningEditDisplayDriver : DisplayDriver<RequiredLearningEditViewModel>
{
    internal readonly IStringLocalizer S;

    public RequiredLearningEditDisplayDriver(IStringLocalizer<RequiredLearningEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(RequiredLearningEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<RequiredLearningEditViewModel>("RequiredLearningRequirement_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Requirement"], "10")),
            Initialize<RequiredLearningEditViewModel>("RequiredLearningAudience_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Who Is Required"], "20")),
            Initialize<RequiredLearningEditViewModel>("RequiredLearningDue_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Due Date and Reminders"], "30")),
            Initialize<RequiredLearningEditViewModel>("RequiredLearningStatus_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "4").Card(S["Status"], "40")));
    }

    public override async Task<IDisplayResult> UpdateAsync(RequiredLearningEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            m => m.Name,
            m => m.Enabled,
            m => m.TargetKind,
            m => m.TargetId,
            m => m.AudienceName,
            m => m.UserNames,
            m => m.DueRule,
            m => m.DueDate,
            m => m.DueDays,
            m => m.ReminderDays,
            m => m.EscalateAfterDays);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the script of the page selects.
    protected override void BuildPrefix(RequiredLearningEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(RequiredLearningEditViewModel source, RequiredLearningEditViewModel model)
    {
        model.Id = source.Id;
        model.Name = source.Name;
        model.Enabled = source.Enabled;
        model.TargetKind = source.TargetKind;
        model.TargetId = source.TargetId;
        model.AudienceName = source.AudienceName;
        model.UserNames = source.UserNames;
        model.DueRule = source.DueRule;
        model.DueDate = source.DueDate;
        model.DueDays = source.DueDays;
        model.ReminderDays = source.ReminderDays;
        model.EscalateAfterDays = source.EscalateAfterDays;
        model.Targets = source.Targets;
        model.Audiences = source.Audiences;
    }
}
