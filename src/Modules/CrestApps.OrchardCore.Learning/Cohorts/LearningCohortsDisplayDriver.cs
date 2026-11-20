using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Cohorts;

/// <summary>
/// The Cohorts card of the editor of a course or a learning path: the cohorts it is linked to, chosen in a picker, and
/// the ones the sync applied, with how many people are in each. Only people who may manage enrollments see it, since
/// linking a cohort enrolls everyone in it.
/// </summary>
internal sealed class LearningCohortsDisplayDriver : ContentDisplayDriver
{
    private readonly CohortLearningSync _sync;
    private readonly ICohortService _cohorts;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LearningCohortsDisplayDriver(
        CohortLearningSync sync,
        ICohortService cohorts,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _sync = sync;
        _cohorts = cohorts;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override void BuildPrefix(ContentItem model, string htmlFieldPrefix)
    {
        Prefix = string.IsNullOrEmpty(htmlFieldPrefix) ? "LearningCohorts" : $"{htmlFieldPrefix}.LearningCohorts";
    }

    public override async Task<IDisplayResult> EditAsync(ContentItem contentItem, BuildEditorContext context)
    {
        if (!await CanEditAsync(contentItem))
        {
            return null;
        }

        return Initialize<LearningCohortsEditViewModel>("LearningCohortsPart_Edit", async model =>
        {
            var selected = contentItem.TryGet<LearningCohortsPart>(out var part) ? part.CohortIds ?? [] : [];
            var link = string.IsNullOrEmpty(contentItem.ContentItemId) ? null : await _sync.FindLinkAsync(contentItem.ContentItemId);
            var cohorts = await _cohorts.ListAsync();

            model.Rendered = true;
            model.CohortIds = [.. selected];
            model.Options = cohorts
                .Select(cohort => new SelectListItem(cohort.Name, cohort.CohortId, selected.Contains(cohort.CohortId)))
                .ToList();

            foreach (var cohortId in link?.CohortIds ?? [])
            {
                var cohort = cohorts.FirstOrDefault(candidate => candidate.CohortId == cohortId) ?? await _cohorts.FindByIdAsync(cohortId);

                model.Linked.Add(new LearningCohortRow
                {
                    CohortId = cohortId,
                    Name = cohort?.Name ?? cohortId,
                    MemberCount = cohort is null ? 0 : await _cohorts.CountMembersAsync(cohortId),
                });
            }

            model.Pending = !model.CohortIds.Order(StringComparer.Ordinal).SequenceEqual((link?.CohortIds ?? []).Order(StringComparer.Ordinal));
        }).Location("Parts:after");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentItem contentItem, UpdateEditorContext context)
    {
        if (!await CanEditAsync(contentItem))
        {
            return null;
        }

        var model = new LearningCohortsEditViewModel();

        // A form without the card (an API call, a quick create) leaves the links as they are.
        if (!await context.Updater.TryUpdateModelAsync(model, Prefix) || !model.Rendered)
        {
            return await EditAsync(contentItem, context);
        }

        var known = (await _cohorts.ListAsync()).Select(cohort => cohort.CohortId).ToHashSet(StringComparer.Ordinal);
        var chosen = (model.CohortIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id) && known.Contains(id)).Distinct(StringComparer.Ordinal).ToList();

        if (chosen.Count > 0 || contentItem.Has(nameof(LearningCohortsPart)))
        {
            contentItem.Alter<LearningCohortsPart>(part => part.CohortIds = chosen);
        }

        return await EditAsync(contentItem, context);
    }

    private async Task<bool> CanEditAsync(ContentItem contentItem)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        return _sync.FindTarget(contentItem) is not null
            && user is not null
            && await _authorizationService.AuthorizeAsync(user, LearningPermissions.ManageEnrollments);
    }
}

/// <summary>
/// The Cohorts card of a course or path editor.
/// </summary>
public class LearningCohortsEditViewModel
{
    /// <summary>
    /// Gets or sets whether the card was in the form that was posted.
    /// </summary>
    public bool Rendered { get; set; }

    /// <summary>
    /// Gets or sets the ids of the cohorts chosen.
    /// </summary>
    public string[] CohortIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the cohorts to choose from.
    /// </summary>
    public List<SelectListItem> Options { get; set; } = [];

    /// <summary>
    /// Gets or sets the cohorts the sync applied to the published item.
    /// </summary>
    public List<LearningCohortRow> Linked { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the cohorts chosen differ from the ones applied, so they apply when the item is published.
    /// </summary>
    public bool Pending { get; set; }
}

/// <summary>
/// A cohort applied to a course or path.
/// </summary>
public class LearningCohortRow
{
    /// <summary>
    /// Gets or sets the cohort id.
    /// </summary>
    public string CohortId { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets how many people are in it.
    /// </summary>
    public int MemberCount { get; set; }
}
