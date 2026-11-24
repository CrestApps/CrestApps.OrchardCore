using System.Globalization;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Ledgers;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Certifications.Workflows;

/// <summary>
/// Triggers the education credit workflow events after the request commits.
/// </summary>
internal sealed class CreditsWorkflowObserver : IEducationCreditObserver
{
    public Task AwardedAsync(QuantityLedgerEntry entry, CancellationToken cancellationToken = default)
    {
        var input = new Dictionary<string, object>
        {
            ["EntryId"] = entry.ItemId,
            ["UserId"] = entry.AccountId,
            ["CreditTypeId"] = entry.Bucket,
            ["Amount"] = entry.Amount,
            ["Source"] = entry.Kind,
            ["SourceId"] = entry.SourceRef,
            ["EarnedOn"] = entry.EffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWorkflowManager>().TriggerEventAsync(nameof(EducationCreditsAwardedEvent), input, entry.ItemId));

        return Task.CompletedTask;
    }

    public Task SubmittedAsync(ExternalLearningSubmission submission, CancellationToken cancellationToken = default)
    {
        var input = new Dictionary<string, object>
        {
            ["SubmissionId"] = submission.ItemId,
            ["UserId"] = submission.UserId,
            ["Title"] = submission.Title,
            ["CreditTypeId"] = submission.CreditTypeId,
            ["Amount"] = submission.Amount,
        };

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWorkflowManager>().TriggerEventAsync(nameof(ExternalLearningSubmittedEvent), input, submission.ItemId));

        return Task.CompletedTask;
    }
}
