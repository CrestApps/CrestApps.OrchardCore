using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Edits who may respond to a survey and when, its limits, anonymity and retention. These settings are live: they
/// apply without a new version.
/// </summary>
internal sealed class SurveyAccessDisplayDriver : DisplayDriver<Survey>
{
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public SurveyAccessDisplayDriver(
        ILocalClock localClock,
        IStringLocalizer<SurveyAccessDisplayDriver> stringLocalizer)
    {
        _localClock = localClock;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(Survey survey, BuildEditorContext context)
        => Initialize<SurveyAccessEditViewModel>("SurveyAccess_Edit", async model =>
        {
            var access = survey.Access ?? new SurveyAccessSettings();
            var limits = access.Limits ?? new RespondentLimits();

            model.Audience = access.Audience;
            model.AudienceIds = string.Join(", ", access.AudienceIds ?? []);
            model.Anonymity = access.Anonymity;
            model.WasIdentified = access.Anonymity == AnonymityMode.Identified;
            model.CollectIpAddress = access.CollectIpAddress;
            model.Opens = await LocalAsync(access.OpensUtc);
            model.Closes = await LocalAsync(access.ClosesUtc);
            model.UseSchedule = access.Schedule is not null;
            model.Days = Enum.GetValues<DayOfWeek>().Select(day =>
            {
                var rule = access.Schedule?.Days.GetValueOrDefault(day);

                return new SurveyDayScheduleEditViewModel
                {
                    Day = day,
                    Mode = rule?.Mode ?? DayScheduleMode.AllowAll,
                    From = rule?.From,
                    To = rule?.To,
                };
            }).ToList();
            model.LimitCounts = limits.Counts;
            model.LimitTotal = limits.Total;
            model.LimitPerDay = limits.PerDay;
            model.LimitPerWeek = limits.PerWeek;
            model.LimitPerMonth = limits.PerMonth;
            model.CooldownMinutes = limits.CooldownMinutes;
            model.Uniqueness = limits.Uniqueness;
            model.MaxResponses = access.MaxResponses;
            model.RequireCaptcha = access.RequireCaptcha;
            model.IncompleteExpiryHours = access.IncompleteExpiryHours;
            model.MinimumGroupSize = access.MinimumGroupSize;
            model.RetentionDays = survey.Retention?.Days;
            model.RetentionAction = survey.Retention?.Action ?? RetentionAction.Delete;
        }).Location("Content:5").OnGroup(SurveyDetailsDisplayDriver.GroupId);

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(Survey survey, UpdateEditorContext context)
    {
        if (context.GroupId != SurveyDetailsDisplayDriver.GroupId)
        {
            return null;
        }

        var model = new SurveyAccessEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var access = survey.Access ??= new SurveyAccessSettings();

        access.Audience = model.Audience;
        access.AudienceIds = (model.AudienceIds ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToList();
        access.Anonymity = model.Anonymity;
        access.CollectIpAddress = model.Anonymity == AnonymityMode.Identified && model.CollectIpAddress;
        access.OpensUtc = await UtcAsync(model.Opens);
        access.ClosesUtc = await UtcAsync(model.Closes);

        if (access.OpensUtc is { } opens && access.ClosesUtc is { } closes && closes <= opens)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Closes), S["The survey must close after it opens."]);
        }

        if (model.UseSchedule)
        {
            access.Schedule = new WeeklySchedule();

            foreach (var day in model.Days ?? [])
            {
                if (day.Mode == DayScheduleMode.Between && (day.From is null || day.To is null || day.To <= day.From))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.Days), S["Each day with a time range needs a start before its end."]);
                }

                access.Schedule.Days[day.Day] = new DaySchedule { Mode = day.Mode, From = day.From, To = day.To };
            }
        }
        else
        {
            access.Schedule = null;
        }

        access.Limits = new RespondentLimits
        {
            Counts = model.LimitCounts,
            Total = Positive(model.LimitTotal),
            PerDay = Positive(model.LimitPerDay),
            PerWeek = Positive(model.LimitPerWeek),
            PerMonth = Positive(model.LimitPerMonth),
            CooldownMinutes = Positive(model.CooldownMinutes),
            Uniqueness = model.Uniqueness,
        };
        access.MaxResponses = Positive(model.MaxResponses);
        access.RequireCaptcha = model.RequireCaptcha;
        access.IncompleteExpiryHours = Math.Max(1, model.IncompleteExpiryHours);
        access.MinimumGroupSize = Math.Max(0, model.MinimumGroupSize);

        survey.Retention = new SurveyRetentionSettings
        {
            Days = Positive(model.RetentionDays),
            Action = model.RetentionAction,
        };

        return Edit(survey, context);
    }

    private async Task<DateTime?> LocalAsync(DateTime? utc)
        => utc is { } value
            ? (await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(value, DateTimeKind.Utc))).DateTime
            : null;

    private async Task<DateTime?> UtcAsync(DateTime? local)
        => local is { } value ? await _localClock.ToUtcLenientAsync(DateTime.SpecifyKind(value, DateTimeKind.Unspecified)) : null;

    private static int? Positive(int? value)
        => value is > 0 ? value : null;
}
