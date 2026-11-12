using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Staff.Core.Services;

/// <summary>
/// What the Staff section of the user editor, or a module that needs a staff record behind a user, sets.
/// </summary>
public sealed class StaffAccountInput
{
    /// <summary>
    /// Gets or sets the employee number.
    /// </summary>
    public string EmployeeNumber { get; set; }

    /// <summary>
    /// Gets or sets the job title.
    /// </summary>
    public string JobTitle { get; set; }

    /// <summary>
    /// Gets or sets how the person is employed.
    /// </summary>
    public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public StaffStatus Status { get; set; } = StaffStatus.Active;

    /// <summary>
    /// Gets or sets the hire date.
    /// </summary>
    public DateOnly? HireDate { get; set; }

    /// <summary>
    /// Gets or sets the main team, joined from today when the person isn't in it yet.
    /// </summary>
    public string PrimaryTeamId { get; set; }

    /// <summary>
    /// Gets or sets the manager.
    /// </summary>
    public string ManagerId { get; set; }

    /// <summary>
    /// Gets or sets the contracted hours a week.
    /// </summary>
    public decimal? ContractedHoursPerWeek { get; set; }

    /// <summary>
    /// Gets or sets the capabilities to add (existing ones stay).
    /// </summary>
    public IReadOnlyCollection<string> AddCapabilities { get; set; } = [];
}

/// <summary>
/// The result of saving a staff member from a user.
/// </summary>
public sealed class StaffAccountResult
{
    /// <summary>
    /// Gets the errors.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Gets whether it was saved.
    /// </summary>
    public bool Succeeded => Errors.Count == 0;

    /// <summary>
    /// Gets or sets the staff member.
    /// </summary>
    public StaffMember StaffMember { get; set; }

    /// <summary>
    /// Gets or sets whether the record was created.
    /// </summary>
    public bool Created { get; set; }
}

/// <summary>
/// Creates or updates the staff record of a user: used by the Staff section of the user editor and by modules that need
/// every person doing their work to be staff, such as agents and cashiers.
/// </summary>
public sealed class StaffAccounts
{
    private readonly IStaffMemberManager _manager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaffAccounts"/> class.
    /// </summary>
    /// <param name="manager">The staff manager.</param>
    /// <param name="clock">The clock.</param>
    public StaffAccounts(IStaffMemberManager manager, IClock clock)
    {
        _manager = manager;
        _clock = clock;
    }

    /// <summary>
    /// Saves the staff record of a user from the user editor's values, creating it when needed.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="displayName">The user's name, used until the record takes it from the user.</param>
    /// <param name="input">The values.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    public async Task<StaffAccountResult> SaveAsync(string userId, string displayName, StaffAccountInput input, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentNullException.ThrowIfNull(input);

        var result = new StaffAccountResult();
        var staff = await _manager.FindByUserIdAsync(userId, cancellationToken);
        var isNew = staff is null;

        staff ??= await _manager.NewAsync(cancellationToken: cancellationToken);
        staff.UserId = userId;
        staff.DisplayName ??= displayName;
        staff.EmployeeNumber = string.IsNullOrWhiteSpace(input.EmployeeNumber) ? null : input.EmployeeNumber.Trim();
        staff.JobTitle = string.IsNullOrWhiteSpace(input.JobTitle) ? null : input.JobTitle.Trim();
        staff.EmploymentType = input.EmploymentType;
        staff.Status = input.Status;
        staff.HireDate = input.HireDate;
        staff.ManagerId = string.IsNullOrWhiteSpace(input.ManagerId) ? null : input.ManagerId;
        staff.ContractedHoursPerWeek = input.ContractedHoursPerWeek;

        foreach (var capability in input.AddCapabilities ?? [])
        {
            if (!staff.HasCapability(capability))
            {
                staff.Capabilities.Add(capability);
            }
        }

        JoinTeam(staff, input.PrimaryTeamId, isNew);

        return await SaveAsync(staff, isNew, result, cancellationToken);
    }

    /// <summary>
    /// Makes sure a user has a staff record, creating one with the given defaults when they have none, and adds
    /// capabilities to it. Nothing else on an existing record changes.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="displayName">The user's name, used until the record takes it from the user.</param>
    /// <param name="capabilities">The capabilities to add.</param>
    /// <param name="defaults">Sets values on a new record, such as the job title or team.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    public async Task<StaffAccountResult> EnsureForUserAsync(string userId, string displayName, IReadOnlyCollection<string> capabilities, Action<StaffMember> defaults = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var result = new StaffAccountResult();
        var staff = await _manager.FindByUserIdAsync(userId, cancellationToken);
        var isNew = staff is null;

        if (isNew)
        {
            staff = await _manager.NewAsync(cancellationToken: cancellationToken);
            staff.UserId = userId;
            staff.DisplayName = displayName;
            defaults?.Invoke(staff);
        }

        var changed = isNew;

        foreach (var capability in capabilities ?? [])
        {
            if (!string.IsNullOrWhiteSpace(capability) && !staff.HasCapability(capability))
            {
                staff.Capabilities.Add(capability);
                changed = true;
            }
        }

        if (!changed)
        {
            result.StaffMember = staff;

            return result;
        }

        return await SaveAsync(staff, isNew, result, cancellationToken);
    }

    private void JoinTeam(StaffMember staff, string teamId, bool isNew)
    {
        if (string.IsNullOrEmpty(teamId))
        {
            return;
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow);

        // Joining a team from here starts today; the staff page changes memberships and their dates.
        if (!staff.MembershipsOn(today).Any(membership => membership.TeamId == teamId))
        {
            staff.TeamMemberships.Add(new TeamMembership { TeamId = teamId, EffectiveFrom = isNew ? null : today });
        }

        staff.PrimaryTeamId = teamId;
    }

    private async Task<StaffAccountResult> SaveAsync(StaffMember staff, bool isNew, StaffAccountResult result, CancellationToken cancellationToken)
    {
        var validation = await _manager.ValidateAsync(staff, cancellationToken: cancellationToken);

        result.Errors.AddRange(validation.Errors.Select(error => error.ErrorMessage));

        if (!result.Succeeded)
        {
            return result;
        }

        if (isNew)
        {
            await _manager.CreateAsync(staff, cancellationToken: cancellationToken);
        }
        else
        {
            await _manager.UpdateAsync(staff, cancellationToken: cancellationToken);
        }

        result.StaffMember = staff;
        result.Created = isNew;

        return result;
    }
}
