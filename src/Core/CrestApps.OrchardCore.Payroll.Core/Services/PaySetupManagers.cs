using CrestApps.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// The default <see cref="IPayScheduleManager"/>.
/// </summary>
public sealed class PayScheduleManager : CatalogManager<PaySchedule>, IPayScheduleManager
{
    private readonly PayScheduleStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayScheduleManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public PayScheduleManager(PayScheduleStore store, IEnumerable<ICatalogEntryHandler<PaySchedule>> handlers, ILogger<CatalogManager<PaySchedule>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PaySchedule>> ListEnabledAsync(CancellationToken cancellationToken = default)
        => (await _store.ListAsync(cancellationToken)).Where(item => item.Enabled).ToList();
}

/// <summary>
/// The default <see cref="IPayCodeManager"/>.
/// </summary>
public sealed class PayCodeManager : CatalogManager<PayCode>, IPayCodeManager
{
    private readonly PayCodeStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayCodeManager"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="logger">The logger.</param>
    public PayCodeManager(PayCodeStore store, IEnumerable<ICatalogEntryHandler<PayCode>> handlers, ILogger<CatalogManager<PayCode>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PayCode>> ListEnabledAsync(CancellationToken cancellationToken = default)
        => (await _store.ListAsync(cancellationToken)).Where(item => item.Enabled).ToList();

    /// <inheritdoc />
    public Task<PayCode> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
        => _store.FindByCodeAsync(code, cancellationToken);
}
