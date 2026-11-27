using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Edits the quotas of a survey definition.
/// </summary>
internal sealed class SurveyDefinitionQuotasDisplayDriver : DisplayDriver<SurveyDefinition>
{
    private readonly ISurveyControlRegistry _registry;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyDefinitionQuotasDisplayDriver"/> class.
    /// </summary>
    /// <param name="registry">The control registry.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyDefinitionQuotasDisplayDriver(
        ISurveyControlRegistry registry,
        IStringLocalizer<SurveyDefinitionQuotasDisplayDriver> stringLocalizer)
    {
        _registry = registry;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(SurveyDefinition definition, BuildEditorContext context)
        => Initialize<SurveyQuotasEditViewModel>("SurveyDefinitionQuotas_Edit", model =>
        {
            model.Quotas = (definition.Quotas ?? []).ToList();
            model.QuotaRules = model.Quotas.Select(quota => SurveyRuleJson.Write(quota.Rule)).ToList();
            model.Endings = (definition.Endings ?? []).ToList();
            model.Sources = SurveyRuleSources.Build(definition, _registry, int.MaxValue);
        }).Location(l => l.Zone("Content", "1").Tab(S["Quotas"], "5").Card(S["Quotas"], "10"));

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(SurveyDefinition definition, UpdateEditorContext context)
    {
        var model = new SurveyQuotasEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var quotas = new List<SurveyQuota>();

        for (var i = 0; i < model.Quotas.Count; i++)
        {
            var quota = model.Quotas[i];

            if (quota is null || (string.IsNullOrWhiteSpace(quota.Title) && quota.Limit <= 0))
            {
                continue;
            }

            quota.Id = string.IsNullOrWhiteSpace(quota.Id) ? IdGenerator.GenerateId() : quota.Id;
            quota.Name = SurveyNames.IsValidVariableCode(quota.Name?.Trim()) ? quota.Name.Trim() : $"quota_{quotas.Count + 1}";
            quota.Title = quota.Title?.Trim();
            quota.Limit = Math.Max(quota.Limit, 0);
            quota.EndingId = quota.Action == QuotaAction.ScreenOut ? quota.EndingId : null;
            quota.Rule = i < model.QuotaRules.Count ? SurveyRuleJson.Read(model.QuotaRules[i]) : null;
            quotas.Add(quota);
        }

        definition.Quotas = quotas;

        return Edit(definition, context);
    }
}
