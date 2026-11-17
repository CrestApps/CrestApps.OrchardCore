using CrestApps.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// Manages pay schedules, so their handlers run.
/// </summary>
public interface IPayScheduleManager : ICatalogManager<PaySchedule>
{
    /// <summary>
    /// Lists the schedules in use, by name.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The schedules.</returns>
    Task<IReadOnlyList<PaySchedule>> ListEnabledAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Manages pay codes, so their handlers run.
/// </summary>
public interface IPayCodeManager : ICatalogManager<PayCode>
{
    /// <summary>
    /// Lists the codes in use, by code.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The codes.</returns>
    Task<IReadOnlyList<PayCode>> ListEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a code by its short code.
    /// </summary>
    /// <param name="code">The code, such as REG.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The code, or <see langword="null"/>.</returns>
    Task<PayCode> FindByCodeAsync(string code, CancellationToken cancellationToken = default);
}

/// <summary>
/// Tells Pay Setup that a module still uses a pay schedule or a pay code, so it is not deleted.
/// </summary>
public interface IPaySetupUsage
{
    /// <summary>
    /// Describes what uses a pay schedule, or returns nothing.
    /// </summary>
    /// <param name="schedule">The schedule.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What uses it.</returns>
    Task<IReadOnlyList<string>> GetUsagesAsync(PaySchedule schedule, CancellationToken cancellationToken = default);

    /// <summary>
    /// Describes what uses a pay code, or returns nothing.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What uses it.</returns>
    Task<IReadOnlyList<string>> GetUsagesAsync(PayCode code, CancellationToken cancellationToken = default);
}
