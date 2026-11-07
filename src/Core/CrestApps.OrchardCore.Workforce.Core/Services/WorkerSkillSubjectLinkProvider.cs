using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Links a worker profile and the user it belongs to as one person for Skills, so a skill given to the user, as Contact
/// Center gives an agent's skills, counts for the worker in scheduling, coverage and dispatch, and the other way around.
/// </summary>
internal sealed class WorkerSkillSubjectLinkProvider : ISkillSubjectLinkProvider
{
    private readonly WorkerStore _workerStore;

    // Per request: coverage and matching ask about the same workers many times.
    private readonly Dictionary<SkillSubject, IReadOnlyList<SkillSubject>> _links = [];

    public WorkerSkillSubjectLinkProvider(WorkerStore workerStore)
    {
        _workerStore = workerStore;
    }

    public async Task<IReadOnlyList<SkillSubject>> GetLinkedSubjectsAsync(SkillSubject subject, CancellationToken cancellationToken = default)
    {
        if (subject.IsEmpty)
        {
            return [];
        }

        if (!_links.TryGetValue(subject, out var links))
        {
            links = await FindLinksAsync(subject, cancellationToken);
            _links[subject] = links;
        }

        return links;
    }

    private async Task<IReadOnlyList<SkillSubject>> FindLinksAsync(SkillSubject subject, CancellationToken cancellationToken)
    {
        if (subject.Kind == SkillSubjectKind.Worker)
        {
            var worker = await _workerStore.FindByIdAsync(subject.Id, cancellationToken);

            return string.IsNullOrEmpty(worker?.UserId) ? [] : [SkillSubject.User(worker.UserId)];
        }

        if (subject.Kind == SkillSubjectKind.User)
        {
            var worker = await _workerStore.FindByUserIdAsync(subject.Id, cancellationToken);

            return worker is null ? [] : [SkillSubject.Worker(worker.ItemId)];
        }

        return [];
    }
}
