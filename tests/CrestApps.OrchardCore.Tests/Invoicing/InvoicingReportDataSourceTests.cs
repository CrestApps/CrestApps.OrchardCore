using System.Security.Claims;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Reports;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.DataSources;
using OrchardCore.Security;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// The invoicing data source reads real invoices and credit notes from the SQLite harness: the fields, the child rows of
/// lines and taxes (issued invoices only), the references to the ledger, and the permission they need.
/// </summary>
public sealed class InvoicingReportDataSourceTests
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Manager");

    [Fact]
    public async Task WithoutViewInvoices_NothingIsExposed()
    {
        // Arrange
        await using var harness = await SeedAsync();

        // Act
        var (dataSets, rows) = await harness.InScopeAsync(async services =>
        {
            var source = Source(services.GetRequiredService<ISession>(), grants: []);
            var context = new DataSourceContext { User = _user };

            return (
                await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken),
                await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.InvoicingSource.Invoices), TestContext.Current.CancellationToken));
        });

        // Assert
        Assert.Empty(dataSets);
        Assert.Empty(rows.Rows);
    }

    [Fact]
    public async Task EachDataSet_ReadsItsRows()
    {
        // Arrange
        await using var harness = await SeedAsync();

        // Act
        var (dataSets, invoices, lines, taxes, creditNotes, schema) = await harness.InScopeAsync(async services =>
        {
            var source = Source(services.GetRequiredService<ISession>());
            var context = new DataSourceContext { User = _user };
            var token = TestContext.Current.CancellationToken;

            return (
                await source.GetDataSetsAsync(context, token),
                Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.InvoicingSource.Invoices), token)),
                Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.InvoicingSource.InvoiceLines), token)),
                Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.InvoicingSource.InvoiceTaxes), token)),
                Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.InvoicingSource.CreditNotes), token)),
                await source.GetSchemaAsync(CommerceReportDataSets.InvoicingSource.Invoices, context, token));
        });

        // Assert
        Assert.Equal(
            [CommerceReportDataSets.InvoicingSource.Invoices, CommerceReportDataSets.InvoicingSource.InvoiceLines, CommerceReportDataSets.InvoicingSource.InvoiceTaxes, CommerceReportDataSets.InvoicingSource.CreditNotes],
            dataSets.Select(dataSet => dataSet.Name));

        // A draft and an issued invoice; the draft has no lines or taxes in the lines and taxes data sets.
        Assert.Equal(2, invoices.Count);
        var issued = Assert.Single(invoices, row => row["Number"] is not null);
        Assert.Equal(("Open", "USD", 100m, 0m, "Fabrikam Ltd."), (issued["Status"], issued["Currency"], issued["Total"], issued["AmountDue"], issued["BillToName"]));

        var line = Assert.Single(lines);
        Assert.Equal((issued["Number"], "Consulting", 2m, 100m), (line["InvoiceNumber"], line["Name"], line["Quantity"], line["Total"]));
        Assert.All(taxes, tax => Assert.Equal(issued["Number"], tax["InvoiceNumber"]));

        var creditNote = Assert.Single(creditNotes);
        Assert.Equal((issued["Number"], "ReduceBalance", 100m), (creditNote["InvoiceNumber"], creditNote["Disposition"], creditNote["Total"]));

        var transaction = Assert.Single(schema.FindField("TransactionId").References);
        Assert.Equal((CommerceReportDataSets.TransactionSource.Name, CommerceReportDataSets.TransactionSource.Transactions), (transaction.Source, transaction.DataSet));
        Assert.True(schema.FindField("BillToId").IsKeyFilterable);
        Assert.Equal(DataFieldType.Date, schema.FindField("DueDate").Type);
    }

    // A draft, and an issued invoice credited in full.
    private static async Task<InvoicingTestHarness> SeedAsync()
    {
        var harness = await InvoicingTestHarness.CreateAsync();
        await harness.CreateDraftAsync();

        var draft = await harness.CreateDraftAsync();
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));
        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        var creditDraft = await harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteCommandService>().CreateDraftAsync(draft.ItemId, TestContext.Current.CancellationToken));
        var credited = await harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteCommandService>().IssueAsync(creditDraft.CreditNote.ItemId, TestContext.Current.CancellationToken));
        Assert.True(credited.Succeeded, string.Join(" ", credited.Errors));

        return harness;
    }

    private static InvoicingReportDataSource Source(ISession session, string[] grants = null)
    {
        var authorization = Authorization(grants ?? [InvoicingPermissions.ViewInvoices.Name]);

        return new InvoicingReportDataSource(
        [
            new InvoicesReportDataSet(session, authorization, new PassThroughStringLocalizer<InvoicesReportDataSet>()),
            new InvoiceLinesReportDataSet(session, authorization, new PassThroughStringLocalizer<InvoiceLinesReportDataSet>()),
            new InvoiceTaxesReportDataSet(session, authorization, new PassThroughStringLocalizer<InvoiceTaxesReportDataSet>()),
            new CreditNotesReportDataSet(session, authorization, new PassThroughStringLocalizer<CreditNotesReportDataSet>()),
        ]);
    }

    private static IAuthorizationService Authorization(string[] grants)
    {
        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().All(requirement => grants.Contains(requirement.Permission.Name))
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return authorizationService.Object;
    }

    private static DataSourceQuery Query(string dataSet)
        => new()
        {
            DataSet = dataSet,
            Fields = new HashSet<string>(StringComparer.Ordinal),
            MaxRows = 100,
            Context = new DataSourceContext { User = _user },
        };

    private static List<Dictionary<string, object>> Rows(DataRowSet table)
        => [.. table.Rows.Select(row => table.Fields.Select((field, index) => (field.Name, Value: row[index])).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal))];
}
