using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DataPipelines.Models;
using OrchardCore.DataPipelines.Steps;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Learning.Pipelines;

/// <summary>
/// A destination step that enrolls the learner of each row in a course, with a Pipeline grant whose source id is the
/// pipeline, so running the pipeline again adds no second grant. Rows it cannot enroll go to the Rejected output with
/// the reason. The person the pipeline runs for needs the Manage enrollments permission.
/// </summary>
public sealed class EnrollLearnersStep : DataPipelineStepType<EnrollLearnersStep, EnrollLearnersStepSettings>, IDataPipelineStepType
{
    /// <summary>
    /// The stable name of the step.
    /// </summary>
    public const string StepName = "LearningEnrollLearners";

    /// <summary>
    /// The name of the output that receives the rows that could not be enrolled.
    /// </summary>
    public const string Rejected = "Rejected";

    /// <summary>
    /// The field added to rejected rows with the reason.
    /// </summary>
    public const string ErrorField = "Error";

    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnrollLearnersStep"/> class.
    /// </summary>
    /// <param name="localizer">The string localizer.</param>
    public EnrollLearnersStep(IStringLocalizer<EnrollLearnersStep> localizer)
    {
        S = localizer;
    }

    /// <summary>
    /// Gets the stable name of the step.
    /// </summary>
    public static string Name => StepName;

    /// <summary>
    /// Gets the name shown in the designer.
    /// </summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<EnrollLearnersStep>("Enroll learners");

    /// <summary>
    /// Gets the description shown in the designer.
    /// </summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<EnrollLearnersStep>("Enrolls the learner of each row in a course.");

    /// <summary>
    /// Gets the category of the step.
    /// </summary>
    public static DataPipelineStepCategory Category => DataPipelineStepCategory.Destination;

    /// <inheritdoc />
    public override IReadOnlyList<DataPipelinePort> GetInputs(DataPipelineStep step)
        => [new(DataPipelinePort.Input, S["Rows"]) { IsRequired = true }];

    /// <inheritdoc />
    public override IReadOnlyList<DataPipelinePort> GetOutputs(DataPipelineStep step)
        => [new(Rejected, S["Rejected"])];

    /// <inheritdoc />
    public override Task DescribeAsync(DataPipelineDescribeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var input = context.GetInputFields();
        var settings = GetSettings(context.Step);

        context.SetOutputFields([.. input, new DataField(ErrorField, S["Error"], DataFieldType.Text)], Rejected);

        foreach (var error in Validate(settings, input))
        {
            context.AddError(error);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task ExecuteAsync(DataPipelineStepContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = GetSettings(context.Step);
        var services = context.Services;
        var user = context.Run.User;

        if (user is null || !await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, LearningPermissions.ManageEnrollments))
        {
            throw new InvalidOperationException(S["The user the pipeline runs for may not manage enrollments."]);
        }

        var enrollmentService = services.GetRequiredService<IEnrollmentService>();
        var userSearch = services.GetRequiredService<IUserSearchService>();
        var rejected = context.GetOutput(Rejected);
        var (enrolled, failed) = (0L, 0L);

        await foreach (var batch in context.GetInput().ReadBatchesAsync(context.CancellationToken))
        {
            var errors = Validate(settings, batch.Fields);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(errors[0]);
            }

            var userIndex = batch.IndexOf(settings.UserIdField);
            var courseIndex = string.IsNullOrEmpty(settings.CourseIdField) ? -1 : batch.IndexOf(settings.CourseIdField);
            var dueIndex = string.IsNullOrEmpty(settings.DueField) ? -1 : batch.IndexOf(settings.DueField);
            var accessIndex = string.IsNullOrEmpty(settings.AccessEndsField) ? -1 : batch.IndexOf(settings.AccessEndsField);
            var userIds = batch.Rows.Select(row => DataValues.ToText(row[userIndex])?.Trim()).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();
            var known = (await userSearch.GetAsync(userIds, context.CancellationToken)).Select(found => found.UserId).ToHashSet(StringComparer.Ordinal);
            var rejectedRows = new List<object[]>();

            foreach (var row in batch.Rows)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var error = await EnrollRowAsync(
                    enrollmentService,
                    id => Task.FromResult(known.Contains(id)),
                    context.Run.PipelineId,
                    DataValues.ToText(row[userIndex]),
                    courseIndex < 0 ? settings.CourseId : DataValues.ToText(row[courseIndex]),
                    dueIndex < 0 ? null : ToUtc(row[dueIndex]),
                    accessIndex < 0 ? null : ToUtc(row[accessIndex]),
                    settings.IgnorePrerequisites);

                if (error is null)
                {
                    enrolled++;
                }
                else
                {
                    failed++;
                    rejectedRows.Add([.. row, error]);
                }
            }

            if (rejectedRows.Count > 0 && rejected.IsConnected)
            {
                await rejected.WriteAsync(new DataBatch(rejected.Fields.Count > 0 ? rejected.Fields : [.. batch.Fields, new DataField(ErrorField, ErrorField, DataFieldType.Text)], rejectedRows), context.CancellationToken);
            }
        }

        if (failed > 0)
        {
            context.LogWarning(S["{0} rows could not be enrolled.", failed]);
        }

        context.AddDelivery(S["Enrolled {0} learners; rejected {1} rows.", enrolled, failed]);
    }

    /// <summary>
    /// Enrolls the learner of one row.
    /// </summary>
    /// <param name="enrollmentService">The enrollment service.</param>
    /// <param name="userExists">Tells whether a user id belongs to a user of the site.</param>
    /// <param name="pipelineId">The pipeline, the source id of the grant.</param>
    /// <param name="userId">The user id of the row.</param>
    /// <param name="courseId">The course of the row.</param>
    /// <param name="dueUtc">The due date of the row.</param>
    /// <param name="accessEndsUtc">The end of access of the row.</param>
    /// <param name="ignorePrerequisites">Whether to skip the course's prerequisites.</param>
    /// <returns>The reason the row was rejected, or <see langword="null"/> when it was enrolled.</returns>
    public static async Task<string> EnrollRowAsync(
        IEnrollmentService enrollmentService,
        Func<string, Task<bool>> userExists,
        string pipelineId,
        string userId,
        string courseId,
        DateTime? dueUtc,
        DateTime? accessEndsUtc,
        bool ignorePrerequisites)
    {
        ArgumentNullException.ThrowIfNull(enrollmentService);
        ArgumentNullException.ThrowIfNull(userExists);

        userId = userId?.Trim();
        courseId = courseId?.Trim();

        if (string.IsNullOrEmpty(userId))
        {
            return "The row has no user id.";
        }

        if (string.IsNullOrEmpty(courseId))
        {
            return "The row has no course.";
        }

        if (!await userExists(userId))
        {
            return "No user has this id.";
        }

        var result = await enrollmentService.EnrollAsync(new EnrollmentRequest
        {
            UserId = userId,
            CourseId = courseId,
            Source = LearningConstants.EnrollmentSources.Pipeline,
            SourceId = pipelineId,
            DueUtc = dueUtc,
            AccessEndsUtc = accessEndsUtc,
            IgnorePrerequisites = ignorePrerequisites,
            Actor = LearningConstants.EnrollmentSources.Pipeline,
        });

        return result.Succeeded ? null : result.ErrorCode switch
        {
            EnrollmentErrors.CourseNotFound => "The course does not exist or is not published.",
            EnrollmentErrors.PrerequisitesNotMet => "The learner has not completed the prerequisite courses.",
            _ => "The learner could not be enrolled: " + result.ErrorCode,
        };
    }

    private List<string> Validate(EnrollLearnersStepSettings settings, IReadOnlyList<DataField> fields)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(settings.UserIdField))
        {
            errors.Add(S["Choose the field that holds the user id of the learner."]);
        }
        else if (!fields.Any(field => field.Name == settings.UserIdField))
        {
            errors.Add(S["The input has no field '{0}'.", settings.UserIdField]);
        }

        if (string.IsNullOrEmpty(settings.CourseId) && string.IsNullOrEmpty(settings.CourseIdField))
        {
            errors.Add(S["Choose the course, or the field that holds it."]);
        }

        foreach (var name in new[] { settings.CourseIdField, settings.DueField, settings.AccessEndsField }.Where(name => !string.IsNullOrEmpty(name)))
        {
            if (!fields.Any(field => field.Name == name))
            {
                errors.Add(S["The input has no field '{0}'.", name]);
            }
        }

        return errors;
    }

    private static DateTime? ToUtc(object value)
        => DataValues.Coerce(value, DataFieldType.DateTime) is DateTime time ? DateTime.SpecifyKind(time, DateTimeKind.Utc) : null;
}
