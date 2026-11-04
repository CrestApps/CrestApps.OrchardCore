using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// What the user editor sets on a person's worker.
/// </summary>
public sealed class UserWorkerInput
{
    /// <summary>
    /// Gets or sets the employee number, which is also the kiosk badge.
    /// </summary>
    public string EmployeeNumber { get; set; }

    /// <summary>
    /// Gets or sets the job title.
    /// </summary>
    public string JobTitle { get; set; }

    /// <summary>
    /// Gets or sets how they are employed.
    /// </summary>
    public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;

    /// <summary>
    /// Gets or sets their status.
    /// </summary>
    public WorkerStatus Status { get; set; } = WorkerStatus.Active;

    /// <summary>
    /// Gets or sets when they were hired.
    /// </summary>
    public DateOnly? HireDate { get; set; }

    /// <summary>
    /// Gets or sets their main team; they join it when they aren't in it yet.
    /// </summary>
    public string PrimaryTeamId { get; set; }

    /// <summary>
    /// Gets or sets their contracted hours a week.
    /// </summary>
    public decimal? ContractedHoursPerWeek { get; set; }
}

/// <summary>
/// The worker of a user account: made and changed from the user editor, with the user's name and email, which it
/// keeps in step with the account. A worker without an account is kept as it is.
/// </summary>
public sealed class WorkerAccounts
{
    private readonly IWorkerManager _workers;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerAccounts"/> class.
    /// </summary>
    public WorkerAccounts(IWorkerManager workers, IClock clock)
    {
        _workers = workers;
        _clock = clock;
    }

    /// <summary>
    /// Makes or changes the worker of a user. It isn't saved to the database until the request ends, so it can run
    /// inside the user editor's own save.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="displayName">The user's name.</param>
    /// <param name="email">The user's email.</param>
    /// <param name="input">What is set.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The worker, or the reasons it can't be saved.</returns>
    public async Task<PlanningResult<WorkerProfile>> SaveAsync(string userId, string displayName, string email, UserWorkerInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new PlanningResult<WorkerProfile>();
        var worker = await _workers.FindByUserIdAsync(userId, cancellationToken);
        var isNew = worker is null;

        worker ??= await _workers.NewAsync(cancellationToken: cancellationToken);
        worker.UserId = userId;
        worker.DisplayName = displayName;
        worker.Email = email;
        worker.EmployeeNumber = string.IsNullOrWhiteSpace(input.EmployeeNumber) ? null : input.EmployeeNumber.Trim();
        worker.JobTitle = string.IsNullOrWhiteSpace(input.JobTitle) ? null : input.JobTitle.Trim();
        worker.EmploymentType = input.EmploymentType;
        worker.Status = input.Status;
        worker.HireDate = input.HireDate;
        worker.ContractedHoursPerWeek = input.ContractedHoursPerWeek;

        if (!string.IsNullOrEmpty(input.PrimaryTeamId))
        {
            var today = DateOnly.FromDateTime(_clock.UtcNow);

            // Joining a team from here starts today; the worker page changes memberships and their dates.
            if (!worker.MembershipsOn(today).Any(membership => membership.TeamId == input.PrimaryTeamId))
            {
                worker.TeamMemberships.Add(new TeamMembership { TeamId = input.PrimaryTeamId, EffectiveFrom = isNew ? null : today });
            }

            worker.PrimaryTeamId = input.PrimaryTeamId;
        }

        var validation = await _workers.ValidateAsync(worker, cancellationToken: cancellationToken);

        foreach (var error in validation.Errors)
        {
            result.Fail(error.ErrorMessage);
        }

        if (!result.Succeeded)
        {
            return result;
        }

        if (isNew)
        {
            await _workers.CreateAsync(worker, cancellationToken: cancellationToken);
        }
        else
        {
            await _workers.UpdateAsync(worker, cancellationToken: cancellationToken);
        }

        result.Value = worker;

        return result;
    }

    /// <summary>
    /// Gives a user's worker the user's name and email, when they changed.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="displayName">The user's name.</param>
    /// <param name="email">The user's email.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether the worker changed.</returns>
    public async Task<bool> RefreshAsync(string userId, string displayName, string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || await _workers.FindByUserIdAsync(userId, cancellationToken) is not { } worker)
        {
            return false;
        }

        if (string.Equals(worker.DisplayName, displayName, StringComparison.Ordinal) && string.Equals(worker.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        worker.DisplayName = string.IsNullOrWhiteSpace(displayName) ? worker.DisplayName : displayName;
        worker.Email = email;
        await _workers.UpdateAsync(worker, cancellationToken: cancellationToken);

        return true;
    }
}
