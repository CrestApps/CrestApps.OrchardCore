using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using CrestApps.OrchardCore.Skills.Reports;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Skills.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Skills.Drivers;

/// <summary>
/// The skill category (competency framework) filter of every report that lists <see cref="SkillsReportFilters.Category"/>
/// in its filter names.
/// </summary>
internal sealed class SkillCategoryReportFilterDisplayDriver : DisplayDriver<ReportFilter>
{
    private readonly IReportManager _reportManager;
    private readonly ISkillManager _skillManager;

    public SkillCategoryReportFilterDisplayDriver(IReportManager reportManager, ISkillManager skillManager)
    {
        _reportManager = reportManager;
        _skillManager = skillManager;
    }

    public override IDisplayResult Edit(ReportFilter filter, BuildEditorContext context)
    {
        if (!Applies(filter.ReportName))
        {
            return null;
        }

        return Initialize<SkillCategoryReportFilterViewModel>("SkillCategoryReportFilter_Edit", async model =>
        {
            model.Category = filter.GetOrDefault<string>(SkillsReportFilters.CategoryKey);
            model.Categories = (await _skillManager.GetAllAsync())
                .Select(skill => skill.Category)
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.CurrentCultureIgnoreCase)
                .Select(category => new SelectListItem(category, category, string.Equals(category, model.Category, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }).Location("Content:2");
    }

    public override async Task<IDisplayResult> UpdateAsync(ReportFilter filter, UpdateEditorContext context)
    {
        if (!Applies(filter.ReportName))
        {
            return null;
        }

        var model = new SkillCategoryReportFilterViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        filter.Set(SkillsReportFilters.CategoryKey, string.IsNullOrWhiteSpace(model.Category) ? null : model.Category.Trim());

        return Edit(filter, context);
    }

    private bool Applies(string reportName)
        => !string.IsNullOrEmpty(reportName)
        && _reportManager.GetReport(reportName) is IReportFilterMetadata metadata
        && metadata.FilterNames.Contains(SkillsReportFilters.Category, StringComparer.Ordinal);
}
