using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.Programs.Services;

/// <summary>
/// Tells learners when learning is required of them, due soon or overdue, and when they complete a path, and tells
/// managers when the required learning of their reports is overdue. Sent after the request commits.
/// </summary>
internal sealed class ProgramsNotificationObserver : IRequiredLearningObserver, ILearningPathObserver
{
    private readonly IServiceProvider _services;
    private readonly ILearningNotificationSender _sender;

    internal readonly IStringLocalizer S;

    public ProgramsNotificationObserver(IServiceProvider services, ILearningNotificationSender sender, IStringLocalizer<ProgramsNotificationObserver> stringLocalizer)
    {
        _services = services;
        _sender = sender;
        S = stringLocalizer;
    }

    public async Task RequiredAsync(RequiredLearningAssignment assignment, LearningRequirement requirement, CancellationToken cancellationToken = default)
    {
        var name = await TargetNameAsync(requirement, cancellationToken);

        Notify(assignment.UserId, assignment.ItemId, ProgramsConstants.NotificationTypes.Required,
            S["Learning required: {0}", name],
            S["{0} is required of you. Open My required learning to start it.", name]);
    }

    public async Task DueSoonAsync(RequiredLearningAssignment assignment, LearningRequirement requirement, int daysBefore, CancellationToken cancellationToken = default)
    {
        var name = await TargetNameAsync(requirement, cancellationToken);

        Notify(assignment.UserId, assignment.ItemId, ProgramsConstants.NotificationTypes.DueSoon,
            S["{0} is due in {1} days", name, daysBefore],
            S["{0}, which is required of you, is due in {1} days. Open My required learning to finish it.", name, daysBefore]);
    }

    public async Task OverdueAsync(RequiredLearningAssignment assignment, LearningRequirement requirement, CancellationToken cancellationToken = default)
    {
        var name = await TargetNameAsync(requirement, cancellationToken);

        Notify(assignment.UserId, assignment.ItemId, ProgramsConstants.NotificationTypes.Overdue,
            S["{0} is overdue", name],
            S["{0}, which is required of you, is overdue. Open My required learning to finish it.", name]);
    }

    public async Task EscalatedAsync(RequiredLearningAssignment assignment, LearningRequirement requirement, IReadOnlyList<string> managerUserIds, CancellationToken cancellationToken = default)
    {
        var name = await TargetNameAsync(requirement, cancellationToken);
        var person = await _services.GetRequiredService<UserManager<IUser>>().FindByIdAsync(assignment.UserId);

        foreach (var managerId in managerUserIds)
        {
            Notify(managerId, assignment.ItemId, ProgramsConstants.NotificationTypes.Overdue,
                S["Required learning of {0} is overdue", person?.UserName ?? assignment.UserId],
                S["{0}, which is required of {1}, is overdue. Open Team learning to see where your team stands.", name, person?.UserName ?? assignment.UserId],
                url: "~/Admin/learning/team");
        }
    }

    public Task CompletedAsync(LearningPathProgress progress, CancellationToken cancellationToken = default)
    {
        Notify(progress.UserId, progress.ItemId, ProgramsConstants.NotificationTypes.PathCompleted,
            S["Learning path completed"],
            S["You completed a learning path. Open My required learning to see your paths."]);

        return Task.CompletedTask;
    }

    private async Task<string> TargetNameAsync(LearningRequirement requirement, CancellationToken cancellationToken)
    {
        var target = _services.GetServices<IRequiredLearningTarget>().FirstOrDefault(candidate => candidate.Kind == requirement?.TargetKind);

        return (target is null ? null : await target.GetDisplayTextAsync(requirement.TargetId, cancellationToken)) ?? requirement?.Name ?? string.Empty;
    }

    private void Notify(string userId, string referenceId, string type, string subject, string body, string url = "~/learning/required")
        => _sender.Send(userId, new LearningNotificationMessage
        {
            Subject = subject,
            Summary = body,
            TextBody = body,
            Type = type,
            ReferenceId = referenceId,
            Url = url,
        });
}
