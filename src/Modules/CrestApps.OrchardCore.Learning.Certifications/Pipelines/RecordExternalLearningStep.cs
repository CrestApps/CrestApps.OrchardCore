using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Certifications.Core;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DataPipelines.Models;
using OrchardCore.DataPipelines.Steps;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Learning.Certifications.Pipelines;

/// <summary>
/// Records the external learning of each row and accepts it, which posts its education credits once per row key.
/// Rows that cannot be recorded go to the Rejected output with the reason.
/// </summary>
public sealed class RecordExternalLearningStep : DataPipelineStepType<RecordExternalLearningStep, RecordExternalLearningStepSettings>, IDataPipelineStepType
{
    public const string StepName = "LearningRecordExternalLearning";

    public const string Rejected = "Rejected";

    public const string ErrorField = "Error";

    private readonly IStringLocalizer S;

    public RecordExternalLearningStep(IStringLocalizer<RecordExternalLearningStep> localizer)
    {
        S = localizer;
    }

    public static string Name => StepName;

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<RecordExternalLearningStep>("Record external learning");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<RecordExternalLearningStep>("Records the external learning of each row as accepted, and posts its education credits.");

    public static DataPipelineStepCategory Category => DataPipelineStepCategory.Destination;

    public override IReadOnlyList<DataPipelinePort> GetInputs(DataPipelineStep step)
        => [new(DataPipelinePort.Input, S["Rows"]) { IsRequired = true }];

    public override IReadOnlyList<DataPipelinePort> GetOutputs(DataPipelineStep step)
        => [new(Rejected, S["Rejected"])];

    public override Task DescribeAsync(DataPipelineDescribeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var input = context.GetInputFields();

        context.SetOutputFields([.. input, new DataField(ErrorField, S["Error"], DataFieldType.Text)], Rejected);

        foreach (var error in Validate(GetSettings(context.Step), input))
        {
            context.AddError(error);
        }

        return Task.CompletedTask;
    }

    public override async Task ExecuteAsync(DataPipelineStepContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = GetSettings(context.Step);
        var services = context.Services;
        var user = context.Run.User;

        if (user is null || !await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, LearningCertificationsPermissions.ReviewExternalLearning))
        {
            throw new InvalidOperationException(S["The user the pipeline runs for may not review external learning."]);
        }

        var credits = services.GetRequiredService<IEducationCreditService>();
        var userSearch = services.GetRequiredService<IUserSearchService>();
        var types = await credits.ListTypesAsync(context.CancellationToken);
        var rejected = context.GetOutput(Rejected);
        var reviewerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var (recorded, skipped, failed) = (0L, 0L, 0L);

        await foreach (var batch in context.GetInput().ReadBatchesAsync(context.CancellationToken))
        {
            var errors = Validate(settings, batch.Fields);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(errors[0]);
            }

            var userIndex = batch.IndexOf(settings.UserIdField);
            var titleIndex = batch.IndexOf(settings.TitleField);
            var amountIndex = batch.IndexOf(settings.AmountField);
            var dateIndex = batch.IndexOf(settings.CompletedOnField);
            var typeIndex = string.IsNullOrEmpty(settings.CreditTypeField) ? -1 : batch.IndexOf(settings.CreditTypeField);
            var providerIndex = string.IsNullOrEmpty(settings.ProviderField) ? -1 : batch.IndexOf(settings.ProviderField);
            var userIds = batch.Rows.Select(row => DataValues.ToText(row[userIndex])?.Trim()).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();
            var known = (await userSearch.GetAsync(userIds, context.CancellationToken)).Select(found => found.UserId).ToHashSet(StringComparer.Ordinal);
            var rejectedRows = new List<object[]>();

            foreach (var row in batch.Rows)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var typeText = typeIndex < 0 ? settings.CreditTypeId : DataValues.ToText(row[typeIndex])?.Trim();
                var type = types.FirstOrDefault(candidate => candidate.ItemId == typeText) ?? types.FirstOrDefault(candidate => string.Equals(candidate.Name, typeText, StringComparison.OrdinalIgnoreCase));
                var (wasRecorded, error) = await RecordRowAsync(
                    credits,
                    S,
                    known,
                    type,
                    DataValues.ToText(row[userIndex])?.Trim(),
                    DataValues.ToText(row[titleIndex])?.Trim(),
                    providerIndex < 0 ? null : DataValues.ToText(row[providerIndex])?.Trim(),
                    DataValues.Coerce(row[amountIndex], DataFieldType.Decimal),
                    DataValues.Coerce(row[dateIndex], DataFieldType.Date),
                    reviewerId,
                    user.Identity?.Name,
                    context.CancellationToken);

                if (error is null)
                {
                    if (wasRecorded)
                    {
                        recorded++;
                    }
                    else
                    {
                        skipped++;
                    }
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
            context.LogWarning(S["{0} rows could not be recorded.", failed]);
        }

        context.AddDelivery(S["Recorded {0} external learning rows; skipped {1} already recorded; rejected {2}.", recorded, skipped, failed]);
    }

    /// <summary>
    /// Records one row as accepted external learning, once: a row recorded before (the same person, credit type, title
    /// and completion date) is skipped, so running the pipeline again posts nothing twice.
    /// </summary>
    /// <returns>Whether a row was recorded, and the reason it was refused, or <see langword="null"/>.</returns>
    internal static async Task<(bool Recorded, string Error)> RecordRowAsync(IEducationCreditService credits, IStringLocalizer S, HashSet<string> knownUsers, EducationCreditType type, string userId, string title, string provider, object amount, object completedOn, string reviewerUserId, string actor, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(userId) || !knownUsers.Contains(userId))
        {
            return (false, S["No user has this id."]);
        }

        if (string.Equals(userId, reviewerUserId, StringComparison.Ordinal))
        {
            return (false, S["The person the pipeline runs for can't record their own external learning."]);
        }

        if (type is null)
        {
            return (false, S["The credit type does not exist."]);
        }

        if (amount is not decimal value || value <= 0)
        {
            return (false, S["The credits must be a number above zero."]);
        }

        if (completedOn is not DateTime day)
        {
            return (false, S["The completion date is missing."]);
        }

        var completed = DateOnly.FromDateTime(day);
        var rowKey = string.Join('|', userId, type.ItemId, title?.Trim().ToUpperInvariant() ?? string.Empty, completed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        if ((await credits.ListExternalForUserAsync(userId, cancellationToken)).Any(existing => existing.RowKey == rowKey))
        {
            return (false, null);
        }

        var submission = new ExternalLearningSubmission
        {
            UserId = userId,
            Title = title,
            Provider = provider,
            CompletedOn = completed,
            CreditTypeId = type.ItemId,
            Amount = value,
            RowKey = rowKey,
        };
        var errors = await credits.SubmitExternalAsync(submission, cancellationToken);

        if (errors.Count > 0)
        {
            return (false, string.Join(" ", errors));
        }

        var result = await credits.AcceptExternalAsync(submission.ItemId, reviewerUserId, actor, S["Recorded by a data pipeline"], cancellationToken);

        return result.Succeeded ? (true, null) : (false, S["The external learning was saved but could not be accepted; review it by hand."]);
    }

    private List<string> Validate(RecordExternalLearningStepSettings settings, IReadOnlyList<DataField> fields)
    {
        var errors = new List<string>();

        foreach (var (name, message) in new[]
        {
            (settings.UserIdField, S["Choose the field that holds the user id."].Value),
            (settings.TitleField, S["Choose the field that holds what the learning was."].Value),
            (settings.AmountField, S["Choose the field that holds the credits."].Value),
            (settings.CompletedOnField, S["Choose the field that holds the completion date."].Value),
        })
        {
            if (string.IsNullOrEmpty(name))
            {
                errors.Add(message);
            }
        }

        if (string.IsNullOrEmpty(settings.CreditTypeId) && string.IsNullOrEmpty(settings.CreditTypeField))
        {
            errors.Add(S["Choose the credit type, or the field that holds it."]);
        }

        foreach (var name in new[] { settings.UserIdField, settings.TitleField, settings.AmountField, settings.CompletedOnField, settings.CreditTypeField, settings.ProviderField }.Where(name => !string.IsNullOrEmpty(name)))
        {
            if (!fields.Any(field => field.Name == name))
            {
                errors.Add(S["The input has no field '{0}'.", name]);
            }
        }

        return errors;
    }
}

/// <summary>
/// The settings of the Record external learning step.
/// </summary>
public sealed class RecordExternalLearningStepSettings
{
    public string UserIdField { get; set; }

    public string TitleField { get; set; }

    public string ProviderField { get; set; }

    public string AmountField { get; set; }

    public string CompletedOnField { get; set; }

    public string CreditTypeId { get; set; }

    public string CreditTypeField { get; set; }
}
