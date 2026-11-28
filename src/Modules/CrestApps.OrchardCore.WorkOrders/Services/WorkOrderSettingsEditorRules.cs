using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.ViewModels;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.WorkOrders.Services;

/// <summary>
/// The rules of the work order type, checklist and sub-status editors: how a record fills its editor's
/// fields, and how the posted fields are applied to the record and checked before the manager validates it. The editors
/// and the AI tools both use them, so a record saved either way is cleaned and checked the same way.
/// </summary>
internal static class WorkOrderSettingsEditorRules
{
    /// <summary>
    /// Fills the type editor's fields from a type.
    /// </summary>
    /// <param name="item">The type.</param>
    /// <param name="id">The type's id, or <see langword="null"/> for a new one.</param>
    /// <returns>The fields.</returns>
    public static WorkOrderTypeEditViewModel FillType(WorkOrderType item, string id)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new WorkOrderTypeEditViewModel
        {
            Id = id,
            Name = item.Name,
            Color = item.Color,
            DefaultDurationMinutes = item.DefaultDurationMinutes,
            CrewSize = item.CrewSize,
            DefaultPriority = item.DefaultPriority,
            ServiceLevelPolicyId = item.ServiceLevelPolicyId,
            BillingMode = item.BillingMode,
            Currency = item.Currency,
            FixedPrice = item.FixedPrice,
            RequiresSignature = item.RequiresSignature,
            RequiredPhotos = item.RequiredPhotos,
            ChecklistTemplateIds = [.. item.ChecklistTemplateIds],
            RequiredSkills = [.. item.RequiredSkills.Select(skill => new SkillRequirement { SkillId = skill.SkillId, MinProficiency = skill.MinProficiency })],
            RequiredCertificationIds = [.. item.RequiredCertificationIds],
            AppointmentTypeIds = [.. item.AppointmentTypeIds],
            Lines = [.. item.DefaultLines.Select(ToEditModel)],
        };
    }

    /// <summary>
    /// Applies the type editor's fields to a type.
    /// </summary>
    /// <param name="item">The type.</param>
    /// <param name="form">The fields.</param>
    /// <param name="S">The editor's string localizer.</param>
    /// <returns>The editor's own refusals, each with its field; the manager's validation comes after.</returns>
    public static IReadOnlyList<(string Field, string Message)> ApplyType(WorkOrderType item, WorkOrderTypeEditViewModel form, IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(S);

        var errors = new List<(string Field, string Message)>();

        item.Name = form.Name?.Trim();
        item.Color = string.IsNullOrWhiteSpace(form.Color) ? null : form.Color.Trim();
        item.DefaultDurationMinutes = form.DefaultDurationMinutes;
        item.CrewSize = form.CrewSize;
        item.DefaultPriority = form.DefaultPriority;
        item.ServiceLevelPolicyId = string.IsNullOrEmpty(form.ServiceLevelPolicyId) ? null : form.ServiceLevelPolicyId;
        item.BillingMode = form.BillingMode;
        item.Currency = string.IsNullOrWhiteSpace(form.Currency) ? "USD" : form.Currency.Trim().ToUpperInvariant();
        item.FixedPrice = form.BillingMode == WorkOrderBillingMode.FixedPrice ? form.FixedPrice : null;
        item.RequiresSignature = form.RequiresSignature;
        item.RequiredPhotos = Math.Max(0, form.RequiredPhotos);
        item.ChecklistTemplateIds = [.. (form.ChecklistTemplateIds ?? []).Where(templateId => !string.IsNullOrEmpty(templateId)).Distinct(StringComparer.Ordinal)];
        item.DefaultLines = [.. (form.Lines ?? []).Where(line => !string.IsNullOrWhiteSpace(line.Description)).Select(line => ToLine(line, null))];
        item.RequiredSkills = [.. (form.RequiredSkills ?? []).Where(skill => !string.IsNullOrEmpty(skill.SkillId)).DistinctBy(skill => skill.SkillId).Select(skill => new SkillRequirement { SkillId = skill.SkillId, MinProficiency = Math.Clamp(skill.MinProficiency, 1, 5) })];
        item.RequiredCertificationIds = [.. (form.RequiredCertificationIds ?? []).Where(certificationId => !string.IsNullOrEmpty(certificationId)).Distinct(StringComparer.Ordinal)];
        item.AppointmentTypeIds = [.. (form.AppointmentTypeIds ?? []).Where(typeId => !string.IsNullOrEmpty(typeId)).Distinct(StringComparer.Ordinal)];

        if (form.BillingMode == WorkOrderBillingMode.FixedPrice && form.FixedPrice is not > 0)
        {
            errors.Add((nameof(form.FixedPrice), S["Enter the fixed price."]));
        }

        return errors;
    }

    /// <summary>
    /// Fills the checklist editor's fields from a checklist.
    /// </summary>
    /// <param name="item">The checklist.</param>
    /// <param name="id">The checklist's id, or <see langword="null"/> for a new one.</param>
    /// <returns>The fields.</returns>
    public static ChecklistTemplateEditViewModel FillChecklist(ChecklistTemplate item, string id)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new ChecklistTemplateEditViewModel
        {
            Id = id,
            Name = item.Name,
            Questions = [.. item.Questions.Select(question => new QuestionEditModel
            {
                Id = question.Id,
                Section = question.Section,
                Text = question.Text,
                Kind = question.Kind,
                Required = question.Required,
                Choices = string.Join('\n', question.Choices),
                Min = question.Min,
                Max = question.Max,
                Unit = question.Unit ?? question.Meter,
                Help = question.Help,
                ShowWhenQuestionId = question.GetRule()?.Conditions.FirstOrDefault()?.Source,
                ShowWhenEquals = question.GetRule()?.Conditions.FirstOrDefault()?.Values.FirstOrDefault(),
            })],
        };
    }

    /// <summary>
    /// Applies the checklist editor's fields to a checklist: rows with no text are dropped, each question keeps only the
    /// limits its kind uses, and a question can only depend on a saved question asked before it.
    /// </summary>
    /// <param name="item">The checklist.</param>
    /// <param name="form">The fields.</param>
    /// <param name="S">The editor's string localizer.</param>
    /// <returns>The editor's own refusals, each with its field; the manager's validation comes after.</returns>
    public static IReadOnlyList<(string Field, string Message)> ApplyChecklist(ChecklistTemplate item, ChecklistTemplateEditViewModel form, IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(S);

        var errors = new List<(string Field, string Message)>();
        var previous = item.Questions.ToDictionary(question => question.Id ?? string.Empty, StringComparer.Ordinal);

        item.Name = form.Name?.Trim();
        item.Questions = [.. (form.Questions ?? []).Where(row => !string.IsNullOrWhiteSpace(row.Text)).Select(row => new ChecklistQuestion
        {
            Id = string.IsNullOrEmpty(row.Id) ? CrestApps.Core.UniqueId.GenerateId() : row.Id,
            Section = string.IsNullOrWhiteSpace(row.Section) ? null : row.Section.Trim(),
            Text = row.Text.Trim(),
            Kind = row.Kind,
            Required = row.Required,
            Choices = row.Kind is ChecklistQuestionKind.Choice or ChecklistQuestionKind.MultiChoice ? [.. (row.Choices ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase)] : [],
            Min = row.Kind is ChecklistQuestionKind.Number or ChecklistQuestionKind.MeterReading ? row.Min : null,
            Max = row.Kind is ChecklistQuestionKind.Number or ChecklistQuestionKind.MeterReading ? row.Max : null,
            Unit = row.Kind == ChecklistQuestionKind.Number && !string.IsNullOrWhiteSpace(row.Unit) ? row.Unit.Trim() : null,
            Meter = row.Kind == ChecklistQuestionKind.MeterReading && !string.IsNullOrWhiteSpace(row.Unit) ? row.Unit.Trim() : null,
            Help = string.IsNullOrWhiteSpace(row.Help) ? null : row.Help.Trim(),
            Rule = ShowWhen(row, previous),
        })];

        // A question shows only after the one it depends on, so it can't depend on itself or on one asked later.
        var positions = item.Questions.Select((question, index) => (question.Id, index)).ToDictionary(entry => entry.Id, entry => entry.index, StringComparer.Ordinal);

        foreach (var question in item.Questions.Where(question => question.Rule is not null))
        {
            if (ConditionalVisibility.Sources(question.Rule).Any(source => !positions.TryGetValue(source, out var at) || at >= positions[question.Id]))
            {
                errors.Add((string.Empty, S["\"{0}\" can only depend on a question asked before it.", question.Text]));
            }
        }

        return errors;
    }

    /// <summary>
    /// Fills the sub-status editor's fields from a sub-status; a new one goes under On hold.
    /// </summary>
    /// <param name="item">The sub-status.</param>
    /// <param name="id">The sub-status's id, or <see langword="null"/> for a new one.</param>
    /// <returns>The fields.</returns>
    public static WorkOrderSubStatusEditViewModel FillSubStatus(WorkOrderSubStatus item, string id)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new WorkOrderSubStatusEditViewModel
        {
            Id = id,
            Name = item.Name,
            ParentStatus = string.IsNullOrEmpty(id) ? WorkOrderStatus.OnHold : item.ParentStatus,
            Color = item.Color,
            PausesSla = item.PausesSla,
        };
    }

    /// <summary>
    /// Applies the sub-status editor's fields to a sub-status.
    /// </summary>
    /// <param name="item">The sub-status.</param>
    /// <param name="form">The fields.</param>
    public static void ApplySubStatus(WorkOrderSubStatus item, WorkOrderSubStatusEditViewModel form)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(form);

        item.Name = form.Name?.Trim();
        item.ParentStatus = form.ParentStatus;
        item.Color = string.IsNullOrWhiteSpace(form.Color) ? null : form.Color.Trim();
        item.PausesSla = form.PausesSla;
    }

    /// <summary>
    /// Gives the editor row of a line.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>The row.</returns>
    public static LineEditModel ToEditModel(WorkOrderLine line) => new()
    {
        Id = line.Id,
        Kind = line.Kind,
        Description = line.Description,
        StockItemId = line.StockItemId,
        Quantity = line.QuantityEstimated,
        UnitPrice = line.UnitPrice,
        Billable = line.Billable,
    };

    /// <summary>
    /// Gives the line of an editor row, keeping what was used on the line it replaces.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="previous">The line it replaces, or <see langword="null"/>.</param>
    /// <returns>The line.</returns>
    public static WorkOrderLine ToLine(LineEditModel row, WorkOrderLine previous) => new()
    {
        Id = previous?.Id ?? (string.IsNullOrEmpty(row.Id) ? null : row.Id),
        Kind = row.Kind,
        Description = row.Description.Trim(),
        StockItemId = row.Kind == WorkOrderLineKind.Product && !string.IsNullOrEmpty(row.StockItemId) ? row.StockItemId : null,
        ProductId = previous?.ProductId,
        QuantityEstimated = Math.Max(0, row.Quantity),
        QuantityUsed = previous?.QuantityUsed ?? 0,
        LocationId = previous?.LocationId,
        UnitPrice = Math.Max(0, row.UnitPrice),
        Billable = row.Billable,
    };

    // The condition a question row asks for: a saved question and the answer that shows it, or none.
    private static ConditionRule ShowWhen(QuestionEditModel row, Dictionary<string, ChecklistQuestion> previous)
        => string.IsNullOrWhiteSpace(row.ShowWhenQuestionId) || row.ShowWhenQuestionId == row.Id || !previous.ContainsKey(row.ShowWhenQuestionId) || string.IsNullOrWhiteSpace(row.ShowWhenEquals)
            ? null
            : new ConditionRule
            {
                Conditions = [new Condition { Source = row.ShowWhenQuestionId, Operator = ConditionOperator.Is, Values = [row.ShowWhenEquals.Trim()] }],
            };
}
