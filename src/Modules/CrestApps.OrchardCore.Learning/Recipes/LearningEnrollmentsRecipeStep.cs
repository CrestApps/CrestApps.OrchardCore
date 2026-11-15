using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Learning.Recipes;

/// <summary>
/// Imports enrollments with their progress. People are matched by user name; a row whose user is not on this site is
/// reported as a recipe error and skipped. Enrollments keep their ids, so a second import updates them.
/// </summary>
internal sealed class LearningEnrollmentsRecipeStep : NamedRecipeStepHandler
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly EnrollmentStore _enrollmentStore;
    private readonly EnrollmentProgressStore _progressStore;
    private readonly LearnerUserLookup _userLookup;
    private readonly CourseEnrollmentKeyStore _keyStore;

    internal readonly IStringLocalizer S;

    public LearningEnrollmentsRecipeStep(
        EnrollmentStore enrollmentStore,
        EnrollmentProgressStore progressStore,
        LearnerUserLookup userLookup,
        CourseEnrollmentKeyStore keyStore,
        IStringLocalizer<LearningEnrollmentsRecipeStep> stringLocalizer)
        : base(LearningConstants.EnrollmentsRecipeStep)
    {
        S = stringLocalizer;
        _enrollmentStore = enrollmentStore;
        _progressStore = progressStore;
        _userLookup = userLookup;
        _keyStore = keyStore;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        // The enrollment keys travel as hashes, never as the keys themselves; they are kept out of the course content.
        if (context.Step["CourseKeys"] is JsonArray keys)
        {
            foreach (var key in keys.OfType<JsonObject>())
            {
                var courseId = key["CourseId"]?.GetValue<string>();
                var hash = key["Hash"]?.GetValue<string>();

                if (string.IsNullOrWhiteSpace(courseId) || string.IsNullOrWhiteSpace(hash))
                {
                    context.Errors.Add(S["An enrollment key has no course or no hash; it was skipped."]);

                    continue;
                }

                await _keyStore.SetHashAsync(courseId, hash);
            }
        }

        if (context.Step["Enrollments"] is not JsonArray rows)
        {
            return;
        }

        foreach (var row in rows.OfType<JsonObject>())
        {
            var userName = row["UserName"]?.GetValue<string>();
            var userId = await _userLookup.FindUserIdAsync(userName);
            var number = row["Number"]?.GetValue<string>() ?? row["ItemId"]?.GetValue<string>();

            if (userId is null)
            {
                context.Errors.Add(S["The user '{0}' of enrollment {1} is not on this site; the enrollment was skipped.", userName, number]);

                continue;
            }

            Enrollment incoming;

            try
            {
                incoming = row.Deserialize<Enrollment>(JsonOptions);
            }
            catch (JsonException exception)
            {
                context.Errors.Add(S["Enrollment {0} could not be read: {1}", number, exception.Message]);

                continue;
            }

            if (incoming is null || string.IsNullOrWhiteSpace(incoming.CourseId))
            {
                context.Errors.Add(S["Enrollment {0} has no course; it was skipped.", number]);

                continue;
            }

            incoming.UserId = userId;

            foreach (var change in incoming.History)
            {
                var actorUserName = row["History"]?.AsArray()?.OfType<JsonObject>()
                    .FirstOrDefault(node => node["ChangedUtc"]?.GetValue<DateTime>() == change.ChangedUtc && node["To"]?.GetValue<string>() == change.To.ToString())?["ActorUserName"]?.GetValue<string>();

                if (actorUserName is not null)
                {
                    change.Actor = await _userLookup.FindUserIdAsync(actorUserName) ?? change.Actor;
                }
            }

            var existing = string.IsNullOrEmpty(incoming.ItemId) ? null : await _enrollmentStore.FindByIdAsync(incoming.ItemId);

            if (existing is null)
            {
                await _enrollmentStore.CreateAsync(incoming);
            }
            else
            {
                // The session tracks the instance it loaded; saving the deserialized copy would store a second document.
                CopyOnto(incoming, existing);
                await _enrollmentStore.SaveAsync(existing);
                incoming = existing;
            }

            if (row["Progress"] is JsonObject progressNode)
            {
                var progress = progressNode.Deserialize<EnrollmentProgress>(JsonOptions) ?? new EnrollmentProgress();
                var current = await _progressStore.FindByEnrollmentAsync(incoming.ItemId);

                progress.ItemId = current?.ItemId ?? progress.ItemId;
                progress.EnrollmentId = incoming.ItemId;
                progress.UserId = userId;
                progress.CourseId = incoming.CourseId;

                if (current is null)
                {
                    await _progressStore.CreateAsync(progress);
                }
                else
                {
                    CopyOnto(progress, current);
                    await _progressStore.SaveAsync(current);
                }
            }
        }
    }

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
