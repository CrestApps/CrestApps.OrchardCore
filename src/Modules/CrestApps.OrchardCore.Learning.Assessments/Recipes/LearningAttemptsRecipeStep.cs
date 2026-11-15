using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Learning.Assessments.Recipes;

/// <summary>
/// Imports quiz attempts, quiz overrides and quiz access codes (as hashes). People are matched by user name; a row whose
/// user is not on this site is reported as a recipe error and skipped. Attempts and overrides keep their ids, so a second
/// import updates them.
/// </summary>
internal sealed class LearningAttemptsRecipeStep : NamedRecipeStepHandler
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly AssessmentAttemptStore _attemptStore;
    private readonly AssessmentOverrideStore _overrideStore;
    private readonly LearningSecretStore _secretStore;
    private readonly LearnerUserLookup _userLookup;

    internal readonly IStringLocalizer S;

    public LearningAttemptsRecipeStep(
        AssessmentAttemptStore attemptStore,
        AssessmentOverrideStore overrideStore,
        LearningSecretStore secretStore,
        LearnerUserLookup userLookup,
        IStringLocalizer<LearningAttemptsRecipeStep> stringLocalizer)
        : base(AssessmentsConstants.AttemptsRecipeStep)
    {
        _attemptStore = attemptStore;
        _overrideStore = overrideStore;
        _secretStore = secretStore;
        _userLookup = userLookup;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step["AccessCodes"] is JsonArray codes)
        {
            foreach (var code in codes.OfType<JsonObject>())
            {
                var quizId = code["QuizId"]?.GetValue<string>();
                var hash = code["Hash"]?.GetValue<string>();

                if (string.IsNullOrWhiteSpace(quizId) || string.IsNullOrWhiteSpace(hash))
                {
                    context.Errors.Add(S["A quiz access code has no quiz or no hash; it was skipped."]);

                    continue;
                }

                await _secretStore.SetHashAsync(AssessmentsConstants.QuizAccessCodeSecret, quizId, hash);
            }
        }

        if (context.Step["Attempts"] is JsonArray attempts)
        {
            foreach (var row in attempts.OfType<JsonObject>())
            {
                var incoming = await ReadAsync<AssessmentAttempt>(context, row, row["Number"]?.GetValue<string>());

                if (incoming is null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(incoming.QuizId))
                {
                    context.Errors.Add(S["Attempt {0} has no quiz; it was skipped.", incoming.Number]);

                    continue;
                }

                foreach (var change in incoming.ScoreHistory)
                {
                    change.Actor = await _userLookup.FindUserIdAsync(row["ScoreHistory"]?.AsArray()?.OfType<JsonObject>()
                        .FirstOrDefault(node => node["ChangedUtc"]?.GetValue<DateTime>() == change.ChangedUtc)?["ActorUserName"]?.GetValue<string>()) ?? change.Actor;
                }

                incoming.GradedBy = await _userLookup.FindUserIdAsync(row["GradedByUserName"]?.GetValue<string>()) ?? incoming.GradedBy;

                var existing = string.IsNullOrEmpty(incoming.ItemId) ? null : await _attemptStore.FindByIdAsync(incoming.ItemId);

                if (existing is null)
                {
                    await _attemptStore.CreateAsync(incoming);
                }
                else
                {
                    CopyOnto(incoming, existing);
                    await _attemptStore.UpdateAsync(existing);
                }
            }
        }

        if (context.Step["Overrides"] is JsonArray overrides)
        {
            foreach (var row in overrides.OfType<JsonObject>())
            {
                var incoming = await ReadAsync<AssessmentOverride>(context, row, row["QuizId"]?.GetValue<string>());

                if (incoming is null)
                {
                    continue;
                }

                incoming.Actor = await _userLookup.FindUserIdAsync(row["ActorUserName"]?.GetValue<string>()) ?? incoming.Actor;

                var existing = string.IsNullOrEmpty(incoming.ItemId) ? null : await _overrideStore.FindByIdAsync(incoming.ItemId);

                if (existing is null)
                {
                    await _overrideStore.CreateAsync(incoming);
                }
                else
                {
                    CopyOnto(incoming, existing);
                    await _overrideStore.UpdateAsync(existing);
                }
            }
        }
    }

    // Reads a row and names its learner by user name; reports and skips a row that cannot be read or whose learner is
    // not on this site.
    private async Task<T> ReadAsync<T>(RecipeExecutionContext context, JsonObject row, string label)
        where T : class
    {
        var userName = row["UserName"]?.GetValue<string>();
        var userId = await _userLookup.FindUserIdAsync(userName);

        if (userId is null)
        {
            context.Errors.Add(S["The user '{0}' of {1} is not on this site; it was skipped.", userName, label]);

            return null;
        }

        T incoming;

        try
        {
            incoming = row.Deserialize<T>(JsonOptions);
        }
        catch (JsonException exception)
        {
            context.Errors.Add(S["{0} could not be read: {1}", label, exception.Message]);

            return null;
        }

        typeof(T).GetProperty("UserId")?.SetValue(incoming, userId);

        return incoming;
    }

    // Copies the imported values onto the instance the session tracks; saving the deserialized copy would store a
    // second document.
    private static void CopyOnto<T>(T source, T target)
    {
        foreach (var property in typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
            {
                property.SetValue(target, property.GetValue(source));
            }
        }
    }
}
