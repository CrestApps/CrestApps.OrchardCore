using CrestApps.OrchardCore.Learning.Programs.Models;

namespace CrestApps.OrchardCore.Learning.Programs.Services;

/// <summary>
/// Applies required learning: works out who a rule covers, starts them on the target with its due date, keeps each
/// person's state, sends reminders and escalations, and records exemptions.
/// </summary>
public interface IRequiredLearningService
{
    /// <summary>
    /// Validates and saves a rule.
    /// </summary>
    /// <returns>The reasons it was refused; empty when it was saved.</returns>
    Task<IReadOnlyList<string>> SaveAsync(LearningRequirement requirement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a rule and its people's states.
    /// </summary>
    Task<bool> DeleteAsync(string requirementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a rule.
    /// </summary>
    Task<LearningRequirement> FindByIdAsync(string requirementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every rule.
    /// </summary>
    Task<IReadOnlyList<LearningRequirement>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists who a rule would cover today, without changing anything.
    /// </summary>
    Task<IReadOnlyList<string>> PreviewAsync(LearningRequirement requirement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a rule now: requires it of people who joined, marks people who left, and works out every state again.
    /// </summary>
    /// <returns>What changed.</returns>
    Task<RequiredLearningRunResult> RunAsync(string requirementId, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Works out again a person's state under every rule about a target, such as when they complete a course.
    /// </summary>
    Task RefreshTargetAsync(string targetKind, string targetId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the reminders and escalations that are due and marks overdue states.
    /// </summary>
    /// <returns>How many reminders and escalations were sent.</returns>
    Task<int> SendRemindersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Exempts a person from a rule.
    /// </summary>
    Task<RequiredLearningAssignment> ExemptAsync(string assignmentId, RequiredLearningExemption exemption, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes an exemption back; the state is worked out again.
    /// </summary>
    Task<RequiredLearningAssignment> RemoveExemptionAsync(string assignmentId, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a person's state.
    /// </summary>
    Task<RequiredLearningAssignment> FindAssignmentAsync(string assignmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the states of some people under every rule, such as a person or a manager's reports.
    /// </summary>
    Task<IReadOnlyList<RequiredLearningAssignment>> ListForUsersAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pages the states under a rule.
    /// </summary>
    Task<(int Count, IReadOnlyList<RequiredLearningAssignment> Items)> PageAsync(string requirementId, RequiredLearningState? state, int skip, int take, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a run of a required learning rule changed.
/// </summary>
/// <param name="Covered">How many people the rule covers.</param>
/// <param name="Added">How many people it was required of now.</param>
/// <param name="Removed">How many people left it.</param>
/// <param name="Changed">How many states changed.</param>
public sealed record RequiredLearningRunResult(int Covered, int Added, int Removed, int Changed);

/// <summary>
/// A kind of target required learning can require, such as a course, a learning path or a certification path.
/// Register one with <c>services.AddRequiredLearningTarget&lt;T&gt;()</c>.
/// </summary>
public interface IRequiredLearningTarget
{
    /// <summary>
    /// Gets the kind, such as <c>Course</c>.
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Gets the name shown.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Lists the targets to choose from.
    /// </summary>
    Task<IReadOnlyList<RequiredLearningTargetOption>> GetOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the name of a target.
    /// </summary>
    Task<string> GetDisplayTextAsync(string targetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a person on the target, with its due date; starting someone already on it changes nothing.
    /// </summary>
    Task<RequiredLearningStartResult> StartAsync(string targetId, string userId, DateTime? dueUtc, string sourceId, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets how far a person is on the target.
    /// </summary>
    Task<RequiredLearningTargetStatus> GetStatusAsync(string targetId, string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// A target to choose from.
/// </summary>
/// <param name="Id">The id.</param>
/// <param name="DisplayText">The name shown.</param>
public sealed record RequiredLearningTargetOption(string Id, string DisplayText);

/// <summary>
/// How far a person is on a target.
/// </summary>
/// <param name="Started">Whether they started it.</param>
/// <param name="CompletedUtc">When they completed it; <see langword="null"/> when they did not.</param>
/// <param name="Assigned">Whether the person has the target at all: an open or completed enrollment, or progress on the
/// path. A run starts anyone required whose target shows nothing.</param>
public sealed record RequiredLearningTargetStatus(bool Started, DateTime? CompletedUtc, bool Assigned = true);

/// <summary>
/// What starting a person on a target did.
/// </summary>
/// <param name="Succeeded">Whether the person now has the target.</param>
/// <param name="Error">Why it was refused, such as a missing prerequisite or a course that does not exist.</param>
public sealed record RequiredLearningStartResult(bool Succeeded, string Error = null)
{
    /// <summary>
    /// The result of a start that worked.
    /// </summary>
    public static RequiredLearningStartResult Success { get; } = new(true);
}
