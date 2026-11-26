using System.Reflection;
using CrestApps.OrchardCore.WorkOrders.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.WorkOrders.Drivers;

/// <summary>
/// Copies the editable members of an editor's view model to the model of one of its shapes. The id is left out: a shape
/// reserves a member of that name, and the pages keep the id in their own hidden field. The editor is left out too.
/// </summary>
/// <typeparam name="TModel">The view model.</typeparam>
internal static class EditorModelCopier<TModel>
{
    private static readonly PropertyInfo[] _properties = [.. typeof(TModel)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property => property.CanRead && property.CanWrite && property.Name is not ("Id" or "Editor"))];

    public static void Copy(TModel source, TModel target)
    {
        foreach (var property in _properties)
        {
            property.SetValue(target, property.GetValue(source));
        }
    }
}

/// <summary>
/// The editor of a work order type: its details, billing, who can do it, the bookings that make it, how a job is finished,
/// its checklists and its default lines, one card each. The fields keep the names the page posts.
/// </summary>
internal sealed class WorkOrderTypeEditDisplayDriver : DisplayDriver<WorkOrderTypeEditViewModel>
{
    internal readonly IStringLocalizer S;

    public WorkOrderTypeEditDisplayDriver(IStringLocalizer<WorkOrderTypeEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(WorkOrderTypeEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<WorkOrderTypeEditViewModel>("WorkOrderTypeDetails_Edit", m => EditorModelCopier<WorkOrderTypeEditViewModel>.Copy(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Details"], "10")),
            Initialize<WorkOrderTypeEditViewModel>("WorkOrderTypeBilling_Edit", m => EditorModelCopier<WorkOrderTypeEditViewModel>.Copy(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Billing"], "20")),
            Initialize<WorkOrderTypeEditViewModel>("WorkOrderTypeWho_Edit", m => EditorModelCopier<WorkOrderTypeEditViewModel>.Copy(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Who Can Do It"], "30"))
                .RenderWhen(() => Task.FromResult(model.SkillOptions.Count + model.CertificationOptions.Count > 0)),
            Initialize<WorkOrderTypeEditViewModel>("WorkOrderTypeBookings_Edit", m => EditorModelCopier<WorkOrderTypeEditViewModel>.Copy(model, m))
                .Location(l => l.Zone("Content", "4").Card(S["Bookings"], "40"))
                .RenderWhen(() => Task.FromResult(model.AppointmentTypeOptions.Count > 0)),
            Initialize<WorkOrderTypeEditViewModel>("WorkOrderTypeFinishing_Edit", m => EditorModelCopier<WorkOrderTypeEditViewModel>.Copy(model, m))
                .Location(l => l.Zone("Content", "5").Card(S["Finishing the Job"], "50")),
            Initialize<WorkOrderTypeEditViewModel>("WorkOrderTypeChecklists_Edit", m => EditorModelCopier<WorkOrderTypeEditViewModel>.Copy(model, m))
                .Location(l => l.Zone("Content", "6").Card(S["Checklists"], "60")),
            Initialize<WorkOrderTypeEditViewModel>("WorkOrderTypeLines_Edit", m => EditorModelCopier<WorkOrderTypeEditViewModel>.Copy(model, m))
                .Location(l => l.Zone("Content", "7").Card(S["Lines Every Work Order Starts With"], "70")));
    }

    public override async Task<IDisplayResult> UpdateAsync(WorkOrderTypeEditViewModel model, UpdateEditorContext context)
    {
        // The rows of the skills and the lines bind with the rest, as they post.
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        return Edit(model, context);
    }

    // The fields keep the names the page posts.
    protected override void BuildPrefix(WorkOrderTypeEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }
}

/// <summary>
/// The editor of a checklist template: its name and its questions, one card each. The fields keep the names the page
/// posts.
/// </summary>
internal sealed class ChecklistTemplateEditDisplayDriver : DisplayDriver<ChecklistTemplateEditViewModel>
{
    internal readonly IStringLocalizer S;

    public ChecklistTemplateEditDisplayDriver(IStringLocalizer<ChecklistTemplateEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ChecklistTemplateEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<ChecklistTemplateEditViewModel>("ChecklistTemplateGeneral_Edit", m => EditorModelCopier<ChecklistTemplateEditViewModel>.Copy(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["General"], "10")),
            Initialize<ChecklistTemplateEditViewModel>("ChecklistTemplateQuestions_Edit", m => EditorModelCopier<ChecklistTemplateEditViewModel>.Copy(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Questions"], "20")));
    }

    public override async Task<IDisplayResult> UpdateAsync(ChecklistTemplateEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        return Edit(model, context);
    }

    // The fields keep the names the page posts.
    protected override void BuildPrefix(ChecklistTemplateEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }
}

/// <summary>
/// The editor of a work order sub-status. The fields keep the names the page posts.
/// </summary>
internal sealed class WorkOrderSubStatusEditDisplayDriver : DisplayDriver<WorkOrderSubStatusEditViewModel>
{
    internal readonly IStringLocalizer S;

    public WorkOrderSubStatusEditDisplayDriver(IStringLocalizer<WorkOrderSubStatusEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(WorkOrderSubStatusEditViewModel model, BuildEditorContext context)
    {
        return Initialize<WorkOrderSubStatusEditViewModel>("WorkOrderSubStatusDetails_Edit", m => EditorModelCopier<WorkOrderSubStatusEditViewModel>.Copy(model, m))
            .Location(l => l.Zone("Content", "1").Card(S["Details"], "10"));
    }

    public override async Task<IDisplayResult> UpdateAsync(WorkOrderSubStatusEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.ParentStatus, m => m.Color, m => m.PausesSla);

        return Edit(model, context);
    }

    // The fields keep the names the page posts.
    protected override void BuildPrefix(WorkOrderSubStatusEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }
}
