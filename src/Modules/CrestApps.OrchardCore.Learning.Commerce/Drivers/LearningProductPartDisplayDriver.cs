using CrestApps.OrchardCore.Learning.Commerce.Models;
using CrestApps.OrchardCore.Learning.Commerce.ViewModels;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Commerce.Drivers;

/// <summary>
/// Edits what buying the product grants.
/// </summary>
internal sealed class LearningProductPartDisplayDriver : ContentPartDisplayDriver<LearningProductPart>
{
    private readonly IContentManager _contentManager;

    public LearningProductPartDisplayDriver(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    public override IDisplayResult Edit(LearningProductPart part, BuildPartEditorContext context)
        => Initialize<LearningProductPartViewModel>(GetEditorShapeType(context), async model =>
        {
            model.Grants = part.Grants.Select(grant => new LearningProductGrantRow { Kind = grant.Kind, TargetId = grant.TargetId, AccessDays = grant.AccessDays, Seats = grant.Seats }).ToList();
            model.EnrollsWithoutKeyOrApproval = part.EnrollsWithoutKeyOrApproval;
            // Only the courses the rows grant: the pickers search the rest on the server.
            model.CourseTitles = (await _contentManager.GetAsync(model.Grants.Select(grant => grant.TargetId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal), VersionOptions.Latest))
                .ToDictionary(course => course.ContentItemId, course => course.DisplayText, StringComparer.Ordinal);
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
