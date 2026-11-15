using System.Runtime.CompilerServices;
using CrestApps.Core;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Payroll.DataSources;

/// <summary>A journal entry with its employer's name.</summary>
public sealed record JournalEntryRow(PayrollJournalEntry Entry, string EmployerName);

/// <summary>
/// Journal entries: two rows (a debit and a credit) per line of each final statement of the employers a user may export
/// payroll data for, with the accounts of the Payroll journal settings and the ledger code of the line's department,
/// filtered by pay date. A void's reversing statement reverses its entries. A Data Pipeline groups them as an accounting
/// system wants and writes its file or sends them to its API.
/// </summary>
public sealed class JournalEntriesDataSet : RecordDataSet<JournalEntryRow>, IPayrollDataSet
{
    private readonly PayrollReportReader _reader;
    private readonly IPayCodeManager _codes;
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _services;

    public JournalEntriesDataSet(PayrollReportReader reader, IPayCodeManager codes, ISiteService siteService, IServiceProvider services, IStringLocalizer<JournalEntriesDataSet> stringLocalizer)
        : base(new DataSetDescriptor("JournalEntries", stringLocalizer["Journal entries"], stringLocalizer["Two rows per line of each final pay statement, a debit and a credit, with the ledger accounts of Settings > Payroll and the department's ledger code. Group them by pay date and account for a journal entry."]))
    {
        _reader = reader;
        _codes = codes;
        _siteService = siteService;
        _services = services;

        var S = stringLocalizer;
        var group = S["Journal entry"].Value;

        AddField("EntryId", S["Entry ID"], DataFieldType.Text, row => $"{row.Entry.StatementId}:{row.Entry.Source}:{row.Entry.Account}:{row.Entry.Debit}:{row.Entry.Credit}", group).Identifier();
        AddField("PayDate", S["Pay date"], DataFieldType.Date, row => row.Entry.PayDate.ToDateTime(TimeOnly.MinValue), group);
        AddField("EmployerId", S["Employer ID"], DataFieldType.Text, row => row.Entry.EmployerId, group);
        AddField("EmployerName", S["Employer"], DataFieldType.Text, row => row.EmployerName, group);
        AddField("PayRunId", S["Pay run ID"], DataFieldType.Text, row => row.Entry.RunId, group);
        AddField("StatementId", S["Statement ID"], DataFieldType.Text, row => row.Entry.StatementId, group);
        AddField("StatementNumber", S["Statement"], DataFieldType.Text, row => row.Entry.StatementNumber, group);
        AddField("Account", S["Account"], DataFieldType.Text, row => row.Entry.Account, group);
        AddField("Department", S["Department"], DataFieldType.Text, row => row.Entry.Department, group);
        AddField("Debit", S["Debit"], DataFieldType.Decimal, row => row.Entry.Debit, group);
        AddField("Credit", S["Credit"], DataFieldType.Decimal, row => row.Entry.Credit, group);
        AddField("Source", S["Pay code or tax"], DataFieldType.Text, row => row.Entry.Source, group);
        AddField("Description", S["Description"], DataFieldType.Text, row => row.Entry.Description, group);
        AddField("Currency", S["Currency"], DataFieldType.Text, row => row.Entry.Currency, group);

        Descriptor.DefaultDateField = "PayDate";
    }

    /// <inheritdoc />
    public override async Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
        => !(await _reader.GetScopeAsync(context?.User, PayrollPermissions.ExportPayrollData, cancellationToken)).IsEmpty;

    protected override async IAsyncEnumerable<JournalEntryRow> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var scope = await _reader.GetScopeAsync(query?.Context?.User, PayrollPermissions.ExportPayrollData, cancellationToken);

        if (scope.IsEmpty)
        {
            yield break;
        }

        var (from, to) = DataConditions.DateRange(query.Conditions ?? [], "PayDate");
        var statements = await _reader.ListStatementsAsync(scope, from.HasValue ? DateOnly.FromDateTime(from.Value) : null, to.HasValue ? DateOnly.FromDateTime(to.Value) : null, cancellationToken);
        var codes = (await _codes.GetAllAsync(cancellationToken)).ToDictionary(code => code.ItemId, StringComparer.Ordinal);
        var settings = await _siteService.GetSettingsAsync<PayrollJournalSettings>();
        var employers = await _reader.EmployerNamesAsync(cancellationToken);
        var ledgerCodes = await LedgerCodesAsync(cancellationToken);

        foreach (var statement in statements.OrderBy(statement => statement.PayDate).ThenBy(statement => statement.Number, StringComparer.Ordinal))
        {
            foreach (var entry in PayrollJournal.EntriesFor(statement, codes, settings, teamId => ledgerCodes.GetValueOrDefault(teamId)))
            {
                yield return new JournalEntryRow(entry, employers.GetValueOrDefault(statement.EmployerId));
            }
        }
    }

    // The ledger code of each team, with Teams on.
    private async Task<Dictionary<string, string>> LedgerCodesAsync(CancellationToken cancellationToken)
    {
        var codes = new Dictionary<string, string>(StringComparer.Ordinal);

        if (_services.GetService<PayrollTeamsFeature>() is null || _services.GetService<ITeamManager>() is not { } teams)
        {
            return codes;
        }

        foreach (var team in await teams.GetAllAsync(cancellationToken))
        {
            if (team.TryGet<PayrollTeamSettings>(out var settings) && !string.IsNullOrEmpty(settings?.LedgerCode))
            {
                codes[team.ItemId] = settings.LedgerCode;
            }
        }

        return codes;
    }
}
