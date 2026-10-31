using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// The YesSql index of workers.
/// </summary>
public sealed class WorkerIndex : CatalogItemIndex, IDisplayTextAwareIndex
{
    /// <summary>
    /// Gets or sets the worker's name.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the name the list searches, the same as <see cref="DisplayName"/>.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the employee number in upper case.
    /// </summary>
    public string NormalizedEmployeeNumber { get; set; }

    /// <summary>
    /// Gets or sets the worker's user id.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the email address.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public WorkerStatus Status { get; set; }

    /// <summary>
    /// Gets or sets how the worker is employed.
    /// </summary>
    public EmploymentType EmploymentType { get; set; }

    /// <summary>
    /// Gets or sets the main team.
    /// </summary>
    public string PrimaryTeamId { get; set; }
}
