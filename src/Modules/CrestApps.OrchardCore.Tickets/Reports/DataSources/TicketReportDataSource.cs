using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Tickets.Reports.DataSources;

internal sealed class TicketReportDataSource : RecordDataSource, IDataSource
{
    private readonly IRecordDataSet[] _dataSets;

    public TicketReportDataSource(
        TicketsReportDataSet tickets,
        TicketEntriesReportDataSet entries,
        TicketChangesReportDataSet changes,
        TicketLinksReportDataSet links,
        TicketBacklogReportDataSet backlog,
        TicketCategoriesReportDataSet categories)
    {
        _dataSets = [tickets, entries, changes, links, backlog, categories];
    }

    public static string Name => TicketReportDataSets.Source;

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<TicketReportDataSource>("Tickets");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<TicketReportDataSource>("The tickets you can see, their replies and notes, every change made to them, their links, each desk's daily backlog and the ticket categories.");

    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
