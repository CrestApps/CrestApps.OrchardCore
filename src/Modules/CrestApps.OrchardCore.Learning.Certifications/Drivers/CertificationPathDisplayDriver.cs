using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Certifications.ViewModels;
using CrestApps.OrchardCore.Skills.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Records;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Certifications.Drivers;

/// <summary>
/// The "Earned through learning" card of the certification editor: the requirements, the reference date, the renewal
/// window and grace period. The path is saved with the certification, never without it.
/// </summary>
internal sealed class CertificationPathDisplayDriver : DisplayDriver<Certification>
{
    private const int EmptyRows = 2;

    private readonly ICertificationPathService _paths;
    private readonly IEnumerable<ICertificationRequirement> _requirements;
    private readonly PendingCertificationPath _pending;
    private readonly ISession _session;
    private readonly IEducationCreditService _credits;

    internal readonly IStringLocalizer S;

    public CertificationPathDisplayDriver(
        ICertificationPathService paths,
        IEnumerable<ICertificationRequirement> requirements,
        PendingCertificationPath pending,
        ISession session,
        IEducationCreditService credits,
        IStringLocalizer<CertificationPathDisplayDriver> stringLocalizer)
    {
        _credits = credits;
        _paths = paths;
        _requirements = requirements;
        _pending = pending;
        _session = session;
        S = stringLocalizer;
    }

    // Every driver of a model gets the model's name as its prefix; this card has its own, so its "Enabled" does not
    // collide with the certification's.
    protected override void BuildPrefix(Certification model, string htmlFieldPrefix)
        => Prefix = string.IsNullOrEmpty(htmlFieldPrefix) ? "CertificationPath" : $"{htmlFieldPrefix}.CertificationPath";

    public override IDisplayResult Edit(Certification certification, BuildEditorContext context)
    {
        return Initialize<CertificationPathViewModel>("CertificationPath_Edit", async model =>
        {
            var path = _pending.Path ?? (context.IsNew ? null : await _paths.FindByCertificationAsync(certification.ItemId));

            if (path is not null)
            {
                model.Earned = true;
                model.PathId = path.ItemId;
                model.Enabled = path.Enabled;
                model.Join = path.Join;
                model.InitialRequirements = path.InitialRequirements.Select(ToRow).ToList();
                model.RenewalRequirements = path.RenewalRequirements.Select(ToRow).ToList();
                model.ReferenceDate = path.ReferenceDate;
                model.FixedMonth = path.FixedMonth;
                model.FixedDay = path.FixedDay;
                model.MinimumIntervalMonths = path.MinimumIntervalMonths;
                model.RenewalWindowDays = path.RenewalWindowDays;
                model.GracePeriodDays = path.GracePeriodDays;
                model.StartsOnFirstRequirement = path.StartsOnFirstRequirement;
                model.EnrollOnRenewal = path.EnrollOnRenewal;
            }

            model.InitialRequirements.AddRange(Enumerable.Range(0, EmptyRows).Select(_ => new CertificationRequirementRow()));
            model.RenewalRequirements.AddRange(Enumerable.Range(0, EmptyRows).Select(_ => new CertificationRequirementRow()));
            model.KindOptions = _requirements.Select(kind => new SelectListItem(kind.DisplayName, kind.Kind)).ToList();
            model.TargetOptions = await TargetOptionsAsync();
        }).Location("Content:10");
    }

    public override async Task<IDisplayResult> UpdateAsync(Certification certification, UpdateEditorContext context)
    {
        var model = new CertificationPathViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var existing = context.IsNew ? null : await _paths.FindByCertificationAsync(certification.ItemId);

        if (!model.Earned)
        {
            // Turning the card off keeps the path and its people, but stops it issuing.
            if (existing is not null && existing.Enabled)
            {
                existing.Enabled = false;
                _pending.Path = existing;
            }

            return Edit(certification, context);
        }

        var path = existing ?? new CertificationPath();
        var kinds = _requirements.Select(kind => kind.Kind).ToHashSet(StringComparer.Ordinal);

        path.Enabled = model.Enabled;
        path.Join = model.Join;
        path.InitialRequirements = ToRequirements(model.InitialRequirements, kinds);
        path.RenewalRequirements = ToRequirements(model.RenewalRequirements, kinds);
        path.ReferenceDate = model.ReferenceDate;
        path.FixedMonth = model.FixedMonth;
        path.FixedDay = model.FixedDay;
        path.MinimumIntervalMonths = model.MinimumIntervalMonths;
        path.RenewalWindowDays = model.RenewalWindowDays;
        path.GracePeriodDays = model.GracePeriodDays;
        path.StartsOnFirstRequirement = model.StartsOnFirstRequirement;
        path.EnrollOnRenewal = model.EnrollOnRenewal;

        if (path.Enabled && path.InitialRequirements.Count == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.InitialRequirements), S["Add at least one requirement, or turn off \"Earned through learning\"."]);
        }

        if (model.RenewalWindowDays < 0 || model.GracePeriodDays < 0 || model.MinimumIntervalMonths < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.RenewalWindowDays), S["Days and months cannot be negative."]);
        }

        if (model.ReferenceDate == CertificationReferenceDate.FixedDate && (model.FixedMonth is < 1 or > 12 || model.FixedDay is < 1 or > 31))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.FixedMonth), S["Choose the month and day every certificate expires on."]);
        }

        if (certification.ValidityMonths is null && path.RenewalRequirements.Count > 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.RenewalRequirements), S["A certification that never expires has no renewal. Set how long it is valid for, or remove the renewal requirements."]);
        }

        _pending.Path = path;

        return Edit(certification, context);
    }

    private static CertificationRequirementRow ToRow(CertificationRequirement requirement)
        => new() { Id = requirement.Id, Kind = requirement.Kind, TargetId = requirement.TargetId, MinimumGrade = requirement.MinimumGrade };

    private static List<CertificationRequirement> ToRequirements(IEnumerable<CertificationRequirementRow> rows, HashSet<string> kinds)
        => (rows ?? [])
            .Where(row => !string.IsNullOrEmpty(row.TargetId) && kinds.Contains(row.Kind ?? string.Empty))
            .Select(row => new CertificationRequirement
            {
                Id = row.Id,
                Kind = row.Kind,
                TargetId = row.TargetId,
                MinimumGrade = row.MinimumGrade is { } grade ? Math.Clamp(grade, 0, 100) : null,
            })
            .ToList();

    private async Task<List<SelectListItem>> TargetOptionsAsync()
    {
        var courseGroup = new SelectListGroup { Name = S["Courses"] };
        var quizGroup = new SelectListGroup { Name = S["Quizzes"] };
        var items = await _session.QueryIndex<ContentItemIndex>(index =>
                (index.ContentType == LearningConstants.ContentTypes.Course || index.ContentType == AssessmentsConstants.ContentTypes.Quiz) && index.Latest)
            .OrderBy(index => index.DisplayText)
            .ListAsync();

        var creditGroup = new SelectListGroup { Name = S["Education credits"] };
        var options = items
            .Select(index => new SelectListItem(index.DisplayText, index.ContentItemId)
            {
                Group = index.ContentType == LearningConstants.ContentTypes.Course ? courseGroup : quizGroup,
            })
            .OrderBy(item => item.Group == courseGroup ? 0 : 1)
            .ToList();

        options.AddRange((await _credits.ListTypesAsync()).Select(type => new SelectListItem(type.Name, type.ItemId) { Group = creditGroup }));

        return options;
    }
}
