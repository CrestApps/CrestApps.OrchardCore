using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Checks each worker's shifts in a schedule against their labor rule set before it is published, and records the
/// overrides when a publish goes ahead with a reason.
/// </summary>
internal sealed class LaborRulesPublishValidator : ISchedulePublishValidator
{
    private readonly ILaborRuleEvaluator _evaluator;
    private readonly ILaborRuleOverrideService _overrides;
    private readonly ActivityCodeStore _activityCodeStore;
    private readonly ShiftTemplateStore _templateStore;

    public LaborRulesPublishValidator(ILaborRuleEvaluator evaluator, ILaborRuleOverrideService overrides, ActivityCodeStore activityCodeStore, ShiftTemplateStore templateStore)
    {
        _evaluator = evaluator;
        _overrides = overrides;
        _activityCodeStore = activityCodeStore;
        _templateStore = templateStore;
    }

    public async Task ValidateAsync(SchedulePublishValidationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var codes = (await _activityCodeStore.GetAllAsync(cancellationToken)).ToDictionary(code => code.ItemId, StringComparer.Ordinal);
        var templates = new Dictionary<string, ShiftTemplate>(StringComparer.Ordinal);

        foreach (var group in context.Drafts.Where(draft => draft.WorkerId is not null).GroupBy(draft => draft.WorkerId, StringComparer.Ordinal))
        {
            if (!context.Workers.TryGetValue(group.Key, out var worker))
            {
                continue;
            }

            var candidates = new List<LaborRuleShift>();

            foreach (var draft in group)
            {
                var template = await TemplateAsync(draft.TemplateId, templates, cancellationToken);

                candidates.Add(new LaborRuleShift
                {
                    ShiftKey = draft.ShiftKey,
                    TeamId = context.Period.TeamId,
                    Date = draft.Date,
                    StartUtc = CalendarTime.ToUtc(draft.Start, context.TimeZoneId),
                    EndUtc = CalendarTime.ToUtc(draft.End, context.TimeZoneId),
                    PaidMinutes = ShiftEvents.PaidMinutes(draft.Start, draft.End, draft.Segments, codes),
                    BreakMinutes = (int)draft.Segments
                        .Where(segment => segment.ActivityCodeId is not null && codes.TryGetValue(segment.ActivityCodeId, out var code) && code.Kind is ActivityKind.Break or ActivityKind.Meal)
                        .Sum(segment => (segment.End - segment.Start).TotalMinutes),
                    RequiredSkillIds = template?.RequiredSkillIds.ToArray() ?? [],
                    IsCandidate = true,
                });
            }

            var result = await _evaluator.EvaluateAsync(new LaborRuleRequest
            {
                Worker = worker,
                TeamId = context.Period.TeamId,
                Candidates = candidates,
                IgnoredShiftKeys = context.PublishedShiftKeys,
                Changes = context.Changes
                    .Where(change => change.WorkerId == worker.ItemId)
                    .Select(change => new LaborRuleChange
                    {
                        Kind = change.Kind,
                        ShiftKey = change.ShiftKey,
                        BeforeStartUtc = change.BeforeStartUtc,
                        AfterStartUtc = change.AfterStartUtc,
                    })
                    .ToList(),
            }, cancellationToken);

            foreach (var violation in result.Violations)
            {
                context.Violations.Add(violation);
            }
        }
    }

    public async Task ProceedingAsync(SchedulePublishValidationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var overridden = context.Violations.Where(violation => violation.Enforcement == LaborRuleEnforcement.RequireOverride).ToArray();

        if (overridden.Length > 0 && !string.IsNullOrWhiteSpace(context.OverrideReason))
        {
            await _overrides.RecordAsync(overridden, context.OverrideReason, "Publish", context.Period.ItemId, context.Period.TeamId, context.Actor, cancellationToken);
        }
    }

    private async Task<ShiftTemplate> TemplateAsync(string templateId, Dictionary<string, ShiftTemplate> templates, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(templateId))
        {
            return null;
        }

        if (!templates.TryGetValue(templateId, out var template))
        {
            template = await _templateStore.FindByIdAsync(templateId, cancellationToken);
            templates[templateId] = template;
        }

        return template;
    }
}
