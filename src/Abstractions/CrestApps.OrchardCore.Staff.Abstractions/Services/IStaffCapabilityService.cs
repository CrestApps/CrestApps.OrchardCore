using CrestApps.OrchardCore.Staff.Models;

namespace CrestApps.OrchardCore.Staff.Services;

/// <summary>
/// The capabilities of the site: the ones enabled modules seed with <c>AddStaffCapability</c>, with the admin's changes,
/// and the ones admins add under Staff &gt; Capabilities. The editor, filters, pickers and settings that offer
/// capabilities read them here.
/// </summary>
public interface IStaffCapabilityService
{
    /// <summary>
    /// Lists the capabilities, ordered for display.
    /// </summary>
    /// <param name="includeDisabled">Whether the ones an admin turned off are listed too.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The capabilities.</returns>
    Task<IReadOnlyList<StaffCapabilityInfo>> ListAsync(bool includeDisabled = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a capability, enabled or not.
    /// </summary>
    /// <param name="name">The capability's key.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The capability, or <see langword="null"/>.</returns>
    Task<StaffCapabilityInfo> FindAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a capability, or saves the changes to one.
    /// </summary>
    /// <param name="definition">The capability.</param>
    /// <param name="isNew">Whether it is being added; a new one can't take the key of an existing one.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The errors; empty when it was saved.</returns>
    Task<IReadOnlyList<string>> SaveAsync(StaffCapabilityDefinition definition, bool isNew, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a capability an admin added, or puts a built-in one back to its module's defaults.
    /// </summary>
    /// <param name="name">The capability's key.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when something was deleted or reset.</returns>
    Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the admin's own capability records, for deployment.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The records.</returns>
    Task<IReadOnlyList<StaffCapabilityDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken = default);
}
