using CrestApps.OrchardCore.Workforce.Models;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Keeps the payroll CSV columns in one document.
/// </summary>
internal sealed class PayrollCsvSettingsStore
{
    private readonly ISession _session;

    public PayrollCsvSettingsStore(ISession session)
    {
        _session = session;
    }

    public async Task<PayrollCsvSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        return await _session.Query<PayrollCsvSettings>(collection: WorkforceConstants.CollectionName).FirstOrDefaultAsync(cancellationToken)
            ?? new PayrollCsvSettings();
    }

    public async Task SaveAsync(PayrollCsvSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var stored = await _session.Query<PayrollCsvSettings>(collection: WorkforceConstants.CollectionName).FirstOrDefaultAsync(cancellationToken);

        if (stored is null)
        {
            stored = settings;
        }
        else if (!ReferenceEquals(stored, settings))
        {
            stored.Columns = settings.Columns;
        }

        await _session.SaveAsync(stored, collection: WorkforceConstants.CollectionName, cancellationToken: cancellationToken);
    }
}
