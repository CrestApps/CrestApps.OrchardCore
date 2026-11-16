using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// Manages pay profiles. Set the tax id through <see cref="IPayProfileService"/>.
/// </summary>
public interface IPayProfileManager : ICatalogManager<EmployeePayProfile>
{
    /// <summary>
    /// Finds the profile of a staff member with an employer.
    /// </summary>
    /// <param name="staffMemberId">The staff member.</param>
    /// <param name="employerId">The employer.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The profile, or <see langword="null"/>.</returns>
    Task<EmployeePayProfile> FindAsync(string staffMemberId, string employerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the profiles of a staff member.
    /// </summary>
    /// <param name="staffMemberId">The staff member.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The profiles.</returns>
    Task<IReadOnlyList<EmployeePayProfile>> ListByStaffMemberAsync(string staffMemberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the profiles of some employers, or every one.
    /// </summary>
    /// <param name="employerIds">The employers, or <see langword="null"/> for every one.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The profiles.</returns>
    Task<IReadOnlyList<EmployeePayProfile>> ListByEmployersAsync(IReadOnlyCollection<string> employerIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Manages pay items.
/// </summary>
public interface IPayItemManager : ICatalogManager<EmployeePayItem>
{
    /// <summary>
    /// Lists the items of a pay profile.
    /// </summary>
    /// <param name="profileId">The profile.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The items.</returns>
    Task<IReadOnlyList<EmployeePayItem>> ListByProfileAsync(string profileId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The protected parts of a pay profile and what keeps it from being paid.
/// </summary>
public interface IPayProfileService
{
    /// <summary>
    /// Sets a person's tax id: protected, with a keyed hash. Empty clears it.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="taxId">The tax id in clear.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task SetTaxIdAsync(EmployeePayProfile profile, string taxId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the other profiles with the same tax id, by its keyed hash.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The other profiles.</returns>
    Task<IReadOnlyList<EmployeePayProfile>> FindSameTaxIdAsync(EmployeePayProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a person's full tax id and records who read it and why. Never logs the number.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="reason">Why it was read.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The tax id, or <see langword="null"/> when it can't be read on this site.</returns>
    Task<string> RevealTaxIdAsync(EmployeePayProfile profile, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists what keeps the profile from being paid, such as a missing tax id or election.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The blockers.</returns>
    Task<IReadOnlyList<PayBlocker>> GetBlockersAsync(EmployeePayProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a user may change a staff member's pay through the admin pages: not their own, unless the settings
    /// allow it.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="staffMemberId">The staff member whose pay changes.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether they may.</returns>
    Task<bool> MayChangePayOfAsync(ClaimsPrincipal user, string staffMemberId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Something that keeps a pay profile from being paid.
/// </summary>
/// <param name="Code">A short code, such as NoTaxId.</param>
/// <param name="Message">What is wrong and what to do.</param>
public sealed record PayBlocker(string Code, string Message);

/// <summary>
/// Adds blockers of pay profiles, such as an unverified bank account. Register with
/// <c>services.TryAddEnumerable(ServiceDescriptor.Scoped&lt;IPayProfileBlockerProvider, T&gt;())</c>.
/// </summary>
public interface IPayProfileBlockerProvider
{
    /// <summary>
    /// Adds the blockers of a profile.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="blockers">The blockers found so far.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task AddBlockersAsync(EmployeePayProfile profile, IList<PayBlocker> blockers, CancellationToken cancellationToken = default);
}

/// <summary>
/// Told about what happens to pay profiles, such as a tax id read, for the Audit Trail. Never given a number.
/// </summary>
public interface IPayProfileObserver
{
    /// <summary>
    /// Tells of a change.
    /// </summary>
    /// <param name="change">The change.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ChangedAsync(PayProfileChange change, CancellationToken cancellationToken = default);
}

/// <summary>
/// A change to a pay profile.
/// </summary>
public sealed class PayProfileChange
{
    /// <summary>
    /// Gets or sets the profile.
    /// </summary>
    public EmployeePayProfile Profile { get; set; }

    /// <summary>
    /// Gets or sets what happened.
    /// </summary>
    public PayProfileChangeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets who did it.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets the user id of who did it.
    /// </summary>
    public string ActorId { get; set; }

    /// <summary>
    /// Gets or sets why.
    /// </summary>
    public string Reason { get; set; }
}

/// <summary>
/// What happened to a pay profile.
/// </summary>
public enum PayProfileChangeKind
{
    /// <summary>
    /// Its tax id was read in full.
    /// </summary>
    TaxIdRevealed,

    /// <summary>
    /// Its tax id changed.
    /// </summary>
    TaxIdChanged,

    /// <summary>
    /// An election was added.
    /// </summary>
    ElectionAdded,

    /// <summary>
    /// The net pay split changed.
    /// </summary>
    DistributionChanged,

    /// <summary>
    /// Something else changed.
    /// </summary>
    Changed,
}
