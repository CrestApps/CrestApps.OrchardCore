using CrestApps.OrchardCore.Learning.Commerce.Models;
using CrestApps.OrchardCore.Learning.Commerce.ViewModels;
using CrestApps.OrchardCore.Learning.Core.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Commerce.Drivers;

/// <summary>
/// Edits what buying the product grants.
/// </summary>
internal sealed class LearningProductPartDisplayDriver : ContentPartDisplayDriver<LearningProductPart>
{
    private readonly CourseLookup _courses;

    public LearningProductPartDisplayDriver(CourseLookup courses)
    {
        _courses = courses;
    }

    public override IDisplayResult Edit(LearningProductPart part, BuildPartEditorContext context)
        => Initialize<LearningProductPartViewModel>(GetEditorShapeType(context), async model =>
        {
            model.Grants = part.Grants.Select(grant => new LearningProductGrantRow { Kind = grant.Kind, TargetId = grant.TargetId, AccessDays = grant.AccessDays, Seats = grant.Seats }).ToList();
            model.EnrollsWithoutKeyOrApproval = part.EnrollsWithoutKeyOrApproval;
            model.Courses = (await _courses.ListCoursesAsync(500)).Select(course => new SelectListItem(course.DisplayText, course.ContentItemId)).ToList();
        });

    public override async Task<IDisplayResult> UpdateAsync(LearningProductPart part, UpdatePartEditorContext context)
    {
        var model = new LearningProductPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, value => value.Grants, value => value.EnrollsWithoutKeyOrApproval);

        part.EnrollsWithoutKeyOrApproval = model.EnrollsWithoutKeyOrApproval;

        part.Grants = (model.Grants ?? [])
            .Where(row => row.Include)
            .Select(row => new LearningProductGrant
            {
                Kind = row.Kind,
                TargetId = string.IsNullOrWhiteSpace(row.TargetId) ? null : row.TargetId,
                AccessDays = row.AccessDays is > 0 ? row.AccessDays : null,
                Seats = row.Seats is > 0 ? row.Seats.Value : 1,
            })
            .ToList();

        return Edit(part, context);
    }
}
