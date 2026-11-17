using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.Localization;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// Stores pay schedules in the Payroll collection. A schedule a module still uses is not deleted.
/// </summary>
public sealed class PayScheduleStore : DocumentCatalog<PaySchedule, PayScheduleIndex>
{
    private readonly IEnumerable<IPaySetupUsage> _usages;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayScheduleStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="usages">What modules use schedules for.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayScheduleStore(ISession session, IEnumerable<IPaySetupUsage> usages, IStringLocalizer<PayScheduleStore> stringLocalizer)
        : base(session)
    {
        CollectionName = PayrollConstants.CollectionName;
        _usages = usages;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists every schedule, by name.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The schedules.</returns>
    public async Task<IReadOnlyList<PaySchedule>> ListAsync(CancellationToken cancellationToken = default)
        => (await Session.Query<PaySchedule, PayScheduleIndex>(collection: CollectionName).OrderBy(index => index.Name).ListAsync(cancellationToken)).ToList();

    /// <inheritdoc />
    protected override async ValueTask DeletingAsync(PaySchedule entry)
    {
        foreach (var usage in _usages)
        {
            if ((await usage.GetUsagesAsync(entry)) is { Count: > 0 } uses)
            {
                throw new InvalidOperationException(S["The pay schedule is in use: {0}", string.Join(", ", uses)]);
            }
        }
    }
}

/// <summary>
/// Stores pay codes in the Payroll collection. A code a module still uses is not deleted.
/// </summary>
public sealed class PayCodeStore : DocumentCatalog<PayCode, PayCodeIndex>
{
    private readonly IEnumerable<IPaySetupUsage> _usages;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayCodeStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="usages">What modules use codes for.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayCodeStore(ISession session, IEnumerable<IPaySetupUsage> usages, IStringLocalizer<PayCodeStore> stringLocalizer)
        : base(session)
    {
        CollectionName = PayrollConstants.CollectionName;
        _usages = usages;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists every code, by code.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The codes.</returns>
    public async Task<IReadOnlyList<PayCode>> ListAsync(CancellationToken cancellationToken = default)
        => (await Session.Query<PayCode, PayCodeIndex>(collection: CollectionName).OrderBy(index => index.Code).ListAsync(cancellationToken)).ToList();

    /// <summary>
    /// Finds a code by its short code.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The code, or <see langword="null"/>.</returns>
    public Task<PayCode> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = code?.Trim().ToUpperInvariant();

        return Session.Query<PayCode, PayCodeIndex>(index => index.Code == normalized, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override async ValueTask DeletingAsync(PayCode entry)
    {
        foreach (var usage in _usages)
        {
            if ((await usage.GetUsagesAsync(entry)) is { Count: > 0 } uses)
            {
                throw new InvalidOperationException(S["The pay code is in use: {0}", string.Join(", ", uses)]);
            }
        }
    }
}
