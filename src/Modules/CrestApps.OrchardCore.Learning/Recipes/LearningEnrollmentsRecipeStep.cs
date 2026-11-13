using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
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

    public LearningEnrollmentsRecipeStep(
        EnrollmentStore enrollmentStore,
        EnrollmentProgressStore progressStore,
        LearnerUserLookup userLookup)
        : base(LearningConstants.EnrollmentsRecipeStep)
    {
        _enrollmentStore = enrollmentStore;
        _progressStore = progressStore;
        _userLookup = userLookup;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
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
                context.Errors.Add($"The user '{userName}' of enrollment {number} is not on this site; the enrollment was skipped.");

                continue;
            }

            Enrollment incoming;

            try
            {
                incoming = row.Deserialize<Enrollment>(JsonOptions);
            }
            catch (JsonException exception)
            {
                context.Errors.Add($"Enrollment {number} could not be read: {exception.Message}");

                continue;
            }

            if (incoming is null || string.IsNullOrWhiteSpace(incoming.CourseId))
            {
                context.Errors.Add($"Enrollment {number} has no course; it was skipped.");

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
                await _enrollmentStore.SaveAsync(incoming);
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
                    await _progressStore.SaveAsync(progress);
                }
            }
        }
    }
}
