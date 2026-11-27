using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Edits who may respond to a survey and when, its limits, anonymity and retention. These settings are live: they
/// apply without a new version.
/// </summary>
internal sealed class SurveyAccessDisplayDriver : DisplayDriver<Survey>
{
    private readonly ILocalClock _localClock;
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    public SurveyAccessDisplayDriver(
        ILocalClock localClock,
        IServiceProvider serviceProvider,
        IStringLocalizer<SurveyAccessDisplayDriver> stringLocalizer)
    {
        _localClock = localClock;
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(Survey survey, BuildEditorContext context)
    {
        // The cards share one view model, so the response count behind it runs once for all of them.
        var hasResponses = new Lazy<Task<bool>>(() => HasResponsesAsync(survey));

        return Combine(
            Initialize<SurveyAccessEditViewModel>("SurveyAccessAudience_Edit", async model => await PopulateAsync(survey, model, hasResponses))
                .Location(l => l.Zone("Content", "5").Card(S["Who May Respond"], "20"))
                .OnGroup(SurveyDetailsDisplayDriver.GroupId),
            Initialize<SurveyAccessEditViewModel>("SurveyAccessAnonymity_Edit", async model => await PopulateAsync(survey, model, hasResponses))
                .Location(l => l.Zone("Content", "6").Card(S["Anonymity"], "30"))
                .OnGroup(SurveyDetailsDisplayDriver.GroupId),
            Initialize<SurveyAccessEditViewModel>("SurveyAccessSchedule_Edit", async model => await PopulateAsync(survey, model, hasResponses))
                .Location(l => l.Zone("Content", "7").Card(S["When"], "40"))
                .OnGroup(SurveyDetailsDisplayDriver.GroupId),
            Initialize<SurveyAccessEditViewModel>("SurveyAccessLimits_Edit", async model => await PopulateAsync(survey, model, hasResponses))
                .Location(l => l.Zone("Content", "8").Card(S["Limits"], "50"))
                .OnGroup(SurveyDetailsDisplayDriver.GroupId),
            Initialize<SurveyAccessEditViewModel>("SurveyAccessRetention_Edit", async model => await PopulateAsync(survey, model, hasResponses))
                .Location(l => l.Zone("Content", "9").Card(S["Retention"], "60"))
                .OnGroup(SurveyDetailsDisplayDriver.GroupId));
    }

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
        access.AudienceIds = (model.AudienceIds ?? []).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).Distinct(StringComparer.Ordinal).ToList();

        if (access.Audience == SurveyAudienceMode.Audiences && access.AudienceIds.Count == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AudienceIds), S["Choose at least one audience, or pick another option for who may respond."]);
        }

        // Anonymity can't change once there are responses: responses given under one promise are never shown under the
        // other.
        if (model.Anonymity != access.Anonymity && await HasResponsesAsync(survey))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Anonymity), S["Anonymity can't change once the survey has responses. Delete its responses first, or copy the survey."]);
        }
        else
        {
            // A survey can only become anonymous with no real responses, but its test responses keep their site-wide
            // numbers, which an anonymous survey never shows next to a named response's number. The page clears them
            // once every editor of the page has validated (SurveysController.AccessPost, SurveyAnonymousNumbers), so a
            // refused save changes no response.
            access.Anonymity = model.Anonymity;
        }

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
        // A CAPTCHA that can't be shown would refuse every guest, so it can only be required while reCAPTCHA is on.
        if (model.RequireCaptcha && !access.RequireCaptcha && !CaptchaRule.IsAvailable(_serviceProvider))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.RequireCaptcha), S["Turn on the reCAPTCHA feature before you require a CAPTCHA."]);
        }

        access.RequireCaptcha = model.RequireCaptcha;
        access.IncompleteExpiryHours = Math.Max(1, model.IncompleteExpiryHours);
        var floor = await FloorAsync();

        if (model.MinimumGroupSize < floor)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MinimumGroupSize), S["The minimum group size can't be below {0}, the smallest the site allows.", floor]);
        }

        access.MinimumGroupSize = Math.Max(floor, model.MinimumGroupSize);

        survey.Retention = new SurveyRetentionSettings
        {
            Days = Positive(model.RetentionDays),
            Action = model.RetentionAction,
        };

        return Edit(survey, context);
    }

    private async Task PopulateAsync(Survey survey, SurveyAccessEditViewModel model, Lazy<Task<bool>> hasResponses)
    {
        var access = survey.Access ?? new SurveyAccessSettings();
        var limits = access.Limits ?? new RespondentLimits();

        model.Audience = access.Audience;
        model.AudienceIds = [.. access.AudienceIds ?? []];
        await BuildAudienceOptionsAsync(model);
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
        model.MinimumGroupSizeFloor = await FloorAsync();
        model.MinimumGroupSize = Math.Max(access.MinimumGroupSize, model.MinimumGroupSizeFloor);
        model.AnonymityLocked = await hasResponses.Value;
        model.CaptchaAvailable = CaptchaRule.IsAvailable(_serviceProvider);
        model.SurveyId = survey.ItemId;
        model.SecretUnreadable = !string.IsNullOrEmpty(survey.ItemId)
            && _serviceProvider.GetService<SurveyAnonymitySecrets>() is { } secrets
            && !secrets.CanReveal(survey);
        model.RetentionDays = survey.Retention?.Days;
        model.RetentionAction = survey.Retention?.Action ?? RetentionAction.Delete;
    }

    // The audiences come from the Users module's User Audiences feature; without it there is nothing to pick.
    private async Task BuildAudienceOptionsAsync(SurveyAccessEditViewModel model)
    {
        var chosen = new HashSet<string>(model.AudienceIds ?? [], StringComparer.Ordinal);

        if (_serviceProvider.GetService<IUserAudienceService>() is { } audiences)
        {
            model.AudiencesAvailable = true;

            foreach (var audience in (await audiences.ListAsync()).OrderBy(audience => audience.DisplayName ?? audience.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                model.AudienceOptions.Add(new SelectListItem(audience.DisplayName ?? audience.Name, audience.Name, chosen.Remove(audience.Name)));
            }
        }

        foreach (var missing in chosen)
        {
            model.AudienceOptions.Add(new SelectListItem(S["{0} (not found)", missing], missing, true));
        }
    }

    private async Task<int> FloorAsync()
        => SurveyAnonymity.Floor(_serviceProvider.GetService<ISiteService>() is { } siteService ? await siteService.GetSettingsAsync<SurveySettings>() : null);

    private async Task<bool> HasResponsesAsync(Survey survey)
    {
        if (string.IsNullOrEmpty(survey?.ItemId) || _serviceProvider.GetService<ISession>() is not { } session)
        {
            return false;
        }

        var surveyId = survey.ItemId;

        return await session.QueryIndex<SurveyResponseIndex>(index => index.SurveyId == surveyId && !index.IsTest, collection: SurveysConstants.ResponsesCollectionName).CountAsync() > 0;
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
