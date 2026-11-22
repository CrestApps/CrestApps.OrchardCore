using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Surveys.Recipes;

/// <summary>
/// Imports surveys with their versions and collectors. A survey is matched by id, then by name. Versions are immutable:
/// a version number the survey already has is skipped, and missing ones are created. Collectors without a token get a
/// new one; the anonymity secret is always made anew.
/// </summary>
internal sealed class SurveysRecipeStep : NamedRecipeStepHandler
{
    private readonly SurveyStore _surveyStore;
    private readonly SurveyVersionStore _versionStore;
    private readonly SurveyCollectorStore _collectorStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly SurveyAnonymitySecrets _secrets;
    private readonly IClock _clock;
    private ISurveyUserLookup _users;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysRecipeStep"/> class.
    /// </summary>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="versionStore">The version store.</param>
    /// <param name="collectorStore">The collector store.</param>
    /// <param name="serviceProvider">The service provider. The user lookup is resolved from it only when a record names a person, so a missing optional service never breaks recipe execution.</param>
    /// <param name="secrets">The anonymity secrets.</param>
    /// <param name="clock">The clock.</param>
    public SurveysRecipeStep(
        SurveyStore surveyStore,
        SurveyVersionStore versionStore,
        SurveyCollectorStore collectorStore,
        IServiceProvider serviceProvider,
        SurveyAnonymitySecrets secrets,
        IClock clock)
        : base(SurveysRecipeSteps.Surveys)
    {
        _surveyStore = surveyStore;
        _versionStore = versionStore;
        _collectorStore = collectorStore;
        _serviceProvider = serviceProvider;
        _secrets = secrets;
        _clock = clock;
    }

    private ISurveyUserLookup UserLookup => _users ??= _serviceProvider.GetRequiredService<ISurveyUserLookup>();

    /// <inheritdoc />
    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        foreach (var token in SurveysRecipeJson.Array(context.Step, SurveysRecipeSteps.SurveysSection))
        {
            var name = SurveysRecipeJson.String(token, nameof(Survey.Name));

            if (string.IsNullOrEmpty(name))
            {
                context.Errors.Add("A survey in the Surveys step has no Name and was skipped.");

                continue;
            }

            name = name.Trim();

            // The same rule as the editor: the name is part of links and lookups, so it is lowercase letters, digits
            // and dashes only.
            if (!SurveyNames.IsValidName(name))
            {
                context.Errors.Add($"The survey name '{name}' in the Surveys step may hold only lowercase letters, digits and dashes; the survey was skipped.");

                continue;
            }

            var id = SurveysRecipeJson.String(token, nameof(Survey.ItemId));
            var survey = (string.IsNullOrEmpty(id) ? null : await _surveyStore.FindByIdAsync(id)) ?? await _surveyStore.FindByNameAsync(name);

            if (survey is not null && !string.Equals(survey.Name, name, StringComparison.Ordinal) && await _surveyStore.FindByNameAsync(name) is { } other && other.ItemId != survey.ItemId)
            {
                context.Errors.Add($"Another survey already uses the name '{name}'; the survey was skipped.");

                continue;
            }
            string[] handled = ["Versions", "Collectors", nameof(Survey.AnonymitySecret), nameof(Survey.PublishedVersionId)];
            var now = _clock.UtcNow;
            var isNew = survey is null;

            if (isNew)
            {
                survey = SurveysRecipeJson.Read<Survey>(token, handled);
                survey.ItemId = !string.IsNullOrEmpty(id) && UniqueId.IsValid(id) ? id : IdGenerator.GenerateId();
                survey.CreatedUtc = now;
                survey.PublishedVersionId = null;
            }
            else
            {
                SurveysRecipeJson.Populate(survey, token, handled);
            }

            survey.Name = name;
            _secrets.EnsureProtected(survey);
            survey.Draft ??= new SurveyDefinition();
            survey.ModifiedUtc = now;

            await ImportVersionsAsync(survey, token, context);
            await ImportCollectorsAsync(survey, token);

            if (isNew)
            {
                await _surveyStore.CreateAsync(survey);
            }
            else
            {
                await _surveyStore.UpdateAsync(survey);
            }
        }
    }

    private async Task ImportVersionsAsync(Survey survey, JsonObject token, RecipeExecutionContext context)
    {
        var existing = (await _versionStore.ListBySurveyAsync(survey.ItemId)).ToDictionary(version => version.Number);

        foreach (var node in SurveysRecipeJson.Array(token, "Versions"))
        {
            var number = SurveysRecipeJson.Int(node, nameof(SurveyVersion.Number)) ?? 0;

            if (number <= 0)
            {
                context.Errors.Add($"A version of the survey '{survey.Name}' has no Number and was skipped.");

                continue;
            }

            // A published version never changes: a number the survey already has is kept as it is.
            if (existing.ContainsKey(number))
            {
                continue;
            }

            var version = SurveysRecipeJson.Read<SurveyVersion>(node, "PublishedByUserName", nameof(SurveyVersion.SurveyId), nameof(SurveyVersion.PublishedBy));
            var versionId = SurveysRecipeJson.String(node, nameof(SurveyVersion.ItemId));

            version.ItemId = !string.IsNullOrEmpty(versionId) && UniqueId.IsValid(versionId) && await _versionStore.FindByIdAsync(versionId) is null
                ? versionId
                : IdGenerator.GenerateId();
            version.SurveyId = survey.ItemId;
            version.Number = number;
            version.Definition ??= new SurveyDefinition();
            version.PublishedBy = await UserLookup.FindUserIdAsync(SurveysRecipeJson.String(node, "PublishedByUserName"));

            if (version.PublishedUtc == default)
            {
                version.PublishedUtc = _clock.UtcNow;
            }

            await _versionStore.CreateAsync(version);
            existing[number] = version;
        }

        // The published version is set last, once every version of the step exists.
        if (survey.PublishedVersionNumber > 0 && existing.TryGetValue(survey.PublishedVersionNumber, out var published))
        {
            survey.PublishedVersionId = published.ItemId;
        }
        else if (survey.PublishedVersionNumber > 0)
        {
            context.Errors.Add($"The survey '{survey.Name}' names published version {survey.PublishedVersionNumber}, which neither the site nor the step has.");
            survey.PublishedVersionNumber = existing.Count > 0 ? existing.Keys.Max() : 0;
            survey.PublishedVersionId = existing.Count > 0 ? existing[survey.PublishedVersionNumber].ItemId : null;
        }
    }

    private async Task ImportCollectorsAsync(Survey survey, JsonObject token)
    {
        var collectors = await _collectorStore.ListBySurveyAsync(survey.ItemId);

        foreach (var node in SurveysRecipeJson.Array(token, "Collectors"))
        {
            var collectorId = SurveysRecipeJson.String(node, nameof(SurveyCollector.ItemId));
            var collectorName = SurveysRecipeJson.String(node, nameof(SurveyCollector.Name));
            var collector = collectors.FirstOrDefault(item => item.ItemId == collectorId)
                ?? collectors.FirstOrDefault(item => !string.IsNullOrEmpty(collectorName) && string.Equals(item.Name, collectorName, StringComparison.OrdinalIgnoreCase));
            var token2 = SurveysRecipeJson.String(node, nameof(SurveyCollector.Token));
            var passwordHash = SurveysRecipeJson.String(node, nameof(SurveyCollector.PasswordHash));

            if (collector is null)
            {
                var imported = SurveysRecipeJson.Read<SurveyCollector>(node, nameof(SurveyCollector.SurveyId));

                imported.ItemId = !string.IsNullOrEmpty(collectorId) && UniqueId.IsValid(collectorId) && await _collectorStore.FindByIdAsync(collectorId) is null
                    ? collectorId
                    : IdGenerator.GenerateId();
                imported.SurveyId = survey.ItemId;
                imported.CreatedUtc = _clock.UtcNow;
                imported.ModifiedUtc = _clock.UtcNow;

                // A token exported without secrets is replaced by a new one, so a link never collides with another site's.
                imported.Token = token2 is not null && await _collectorStore.FindByTokenAsync(token2) is null ? token2 : SurveyTokens.NewCollectorToken();
                imported.PasswordHash = passwordHash;

                await _collectorStore.CreateAsync(imported);

                continue;
            }

            var keepToken = collector.Token;
            var keepHash = collector.PasswordHash;

            SurveysRecipeJson.Populate(collector, node, nameof(SurveyCollector.SurveyId), nameof(SurveyCollector.Token), nameof(SurveyCollector.PasswordHash));
            collector.SurveyId = survey.ItemId;
            collector.ModifiedUtc = _clock.UtcNow;
            collector.Token = token2 is not null && (token2 == keepToken || await _collectorStore.FindByTokenAsync(token2) is null) ? token2 : keepToken;
            collector.PasswordHash = passwordHash ?? keepHash;

            await _collectorStore.UpdateAsync(collector);
        }
    }
}
