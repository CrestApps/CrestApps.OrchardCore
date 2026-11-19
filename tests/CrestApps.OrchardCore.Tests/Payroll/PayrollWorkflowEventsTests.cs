using CrestApps.Core.Models;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Core.UnitedStates;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.Workflows;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// Payroll's workflow events beyond the run: a cancelled run, filings and the payroll year, tax deposits and
/// garnishments, each with the event it starts and the input a workflow reads.
/// </summary>
public sealed class PayrollWorkflowEventsTests
{
    [Fact]
    public void ACanceledRun_StartsPayRunCanceled()
        => Assert.Equal([nameof(PayRunCanceledEvent)], PayrollWorkflowObserver.EventsFor(PayRunChangeKind.Canceled));

    [Theory]
    [InlineData(PayrollFilingChangeKind.Prepared, nameof(PayrollFilingPreparedEvent))]
    [InlineData(PayrollFilingChangeKind.Filed, nameof(PayrollFilingFiledEvent))]
    [InlineData(PayrollFilingChangeKind.Amended, nameof(PayrollFilingAmendedEvent))]
    [InlineData(PayrollFilingChangeKind.YearClosed, nameof(PayrollYearClosedEvent))]
    [InlineData(PayrollFilingChangeKind.YearReopened, nameof(PayrollYearReopenedEvent))]
    public void EachFilingChange_StartsItsEvent(PayrollFilingChangeKind kind, string expected)
        => Assert.Equal(expected, PayrollWorkflowObserver.EventFor(kind));

    [Fact]
    public void AFilingsInput_NamesTheFormPeriodAndWhoFiledIt()
    {
        var filing = new PayrollFiling
        {
            ItemId = "f1",
            EmployerId = "contoso",
            Form = "941",
            Year = 2026,
            Quarter = 4,
            Status = PayrollFilingStatus.Filed,
            FiledOn = new DateOnly(2027, 1, 20),
            Reference = "CONF-1",
            AmendsId = "f0",
            People = [new PayrollFilingPerson(), new PayrollFilingPerson()],
        };

        var input = PayrollWorkflowObserver.FilingInput(new PayrollFilingChange { Kind = PayrollFilingChangeKind.Filed, EmployerId = "contoso", Year = 2026, Filing = filing, Actor = "filer" });

        Assert.Equal(("f1", "contoso", "941", 2026, 4), (input["FilingId"], input["EmployerId"], input["Form"], input["Year"], input["Quarter"]));
        Assert.Equal(("Filed", "2027-01-20", "CONF-1", "f0", 2), (input["Status"], input["FiledOn"], input["Reference"], input["AmendsId"], input["People"]));
        Assert.Equal(("Filed", "filer"), (input["Change"], input["Actor"]));
        Assert.Equal(filing.Period, input["Period"]);

        var year = PayrollWorkflowObserver.FilingInput(new PayrollFilingChange { Kind = PayrollFilingChangeKind.YearClosed, EmployerId = "contoso", Year = 2026, Actor = "filer" });

        Assert.Equal(("contoso", 2026, "YearClosed"), (year["EmployerId"], year["Year"], year["Change"]));
        Assert.False(year.ContainsKey("FilingId"));
    }

    [Fact]
    public async Task TheFilingService_TellsEachPreparationFilingAmendmentAndTheYearClosingAndReopening()
    {
        var observer = new RecordingFilingObserver();

        await using var harness = await PayRunTests.CreateHarnessAsync(services =>
        {
            services.AddPayrollFilingsCore();
            services.AddPayrollFilingForm<Form941>();
            services.AddPayrollFilingForm<Form940>();
            services.AddPayrollFilingForm<FormW2>();
            services.AddSingleton<IPayrollFilingObserver>(observer);
        });
        var setup = await PayRunTests.SetUpAsync(harness);
        var filer = CalendarTestHarness.User("filer", PayrollPermissions.ManagePayrollFilings.Name);

        await harness.RunAsync(async services =>
        {
            var employer = await services.GetRequiredService<IEmployerManager>().FindByIdAsync(setup.EmployerId);
            var service = services.GetRequiredService<IPayrollFilingService>();
            var draft = (await service.PrepareAsync(employer, Form941.FormName, 2026, 4, filer)).Filing;

            Assert.True((await service.MarkFiledAsync(draft, new DateOnly(2027, 1, 20), "CONF-1", filer)).Succeeded);
            Assert.True((await service.AmendAsync(draft, filer)).Succeeded);

            await services.GetRequiredService<IPayRunService>().CancelAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId), "Not needed");

            Assert.True((await service.CloseYearAsync(employer, 2026, filer)).Succeeded);

            await service.ReopenYearAsync(employer, 2026, filer);
        });

        // Closing the year prepares its W-2s first.
        Assert.Equal(
            [PayrollFilingChangeKind.Prepared, PayrollFilingChangeKind.Filed, PayrollFilingChangeKind.Amended, PayrollFilingChangeKind.Prepared, PayrollFilingChangeKind.YearClosed, PayrollFilingChangeKind.YearReopened],
            observer.Changes.Select(change => change.Kind));
        Assert.Equal(("CONF-1", PayrollFilingStatus.Filed), (observer.Changes[1].Filing.Reference, observer.Changes[1].Filing.Status));
        Assert.Equal(observer.Changes[0].Filing.ItemId, observer.Changes[2].Filing.AmendsId);
        Assert.Equal(FormW2.FormName, observer.Changes[3].Filing.Form);
        Assert.All(observer.Changes, change => Assert.Equal((setup.EmployerId, 2026, "filer"), (change.EmployerId, change.Year, change.Actor)));
        Assert.Null(observer.Changes[4].Filing);
    }

    [Theory]
    [InlineData(GarnishmentChangeKind.Withheld, nameof(GarnishmentWithheldEvent))]
    [InlineData(GarnishmentChangeKind.RemittanceVoided, nameof(GarnishmentRemittanceVoidedEvent))]
    [InlineData(GarnishmentChangeKind.Completed, nameof(GarnishmentOrderCompletedEvent))]
    public void EachGarnishmentChange_StartsItsEvent(GarnishmentChangeKind kind, string expected)
        => Assert.Equal(expected, GarnishmentsWorkflowObserver.EventFor(kind));

    [Fact]
    public void AGarnishmentsInput_HasTheOrderTheStatementAndThePayment()
    {
        var order = new GarnishmentOrder { ItemId = "o1", EmployerId = "contoso", StaffMemberId = "lee", Type = GarnishmentType.Support, CaseNumber = "CS-1", PayeeName = "State unit", Status = GarnishmentStatus.Completed, Withheld = 400, TotalOwed = 400 };
        var remittance = new GarnishmentRemittance { StatementId = "s1", StatementNumber = "11", PayDate = new DateOnly(2026, 11, 20), Amount = 400, Method = GarnishmentPaymentMethod.Check, ReferenceId = "check-1", Reference = "Check 1001" };
        var statement = new PayStatement { ItemId = "s1", Number = "11", Currency = "USD" };

        var input = GarnishmentsWorkflowObserver.RemittanceInput(new GarnishmentChange { Kind = GarnishmentChangeKind.Withheld, Order = order, Remittance = remittance, Statement = statement });

        Assert.Equal(("o1", "contoso", "lee", "Support", "CS-1"), (input["OrderId"], input["EmployerId"], input["StaffMemberId"], input["Type"], input["CaseNumber"]));
        Assert.Equal(("Completed", 400m, 0m), (input["Status"], input["Withheld"], input["Remaining"]));
        Assert.Equal(("s1", "11", "2026-11-20", 400m, "USD"), (input["StatementId"], input["StatementNumber"], input["PayDate"], input["Amount"], input["Currency"]));
        Assert.Equal(("Check", "check-1", "Check 1001", "Withheld"), (input["Method"], input["ReferenceId"], input["Reference"], input["Change"]));
    }

    [Fact]
    public void ANewOrderAndADeposit_HaveTheirOwnInput()
    {
        var order = GarnishmentOrderWorkflowHandler.OrderInput(new GarnishmentOrder { ItemId = "o1", EmployerId = "contoso", ProfileId = "p1", Type = GarnishmentType.FederalTaxLevy, PaymentMethod = GarnishmentPaymentMethod.Outside, StartDate = new DateOnly(2026, 1, 1) });
        var deposit = TaxDepositWorkflowHandler.DepositInput(new TaxDeposit { ItemId = "d1", EmployerId = "contoso", Jurisdiction = "US", PeriodStart = new DateOnly(2026, 11, 1), PeriodEnd = new DateOnly(2026, 11, 30), Amount = 1000m, PaidOn = new DateOnly(2026, 12, 10), Reference = "EFTPS-1" });

        Assert.Equal(("o1", "p1", "FederalTaxLevy", "Outside", "2026-01-01"), (order["OrderId"], order["ProfileId"], order["Type"], order["PaymentMethod"], order["StartDate"]));
        Assert.Equal(("d1", "US", "Withholding", "2026-11-01", "2026-11-30"), (deposit["DepositId"], deposit["Jurisdiction"], deposit["Group"], deposit["PeriodStart"], deposit["PeriodEnd"]));
        Assert.Equal((1000m, "2026-12-10", "EFTPS-1"), (deposit["Amount"], deposit["PaidOn"], deposit["Reference"]));
    }

    [Fact]
    public async Task TheCatalogHandlers_StartNothingWithoutAShellScope()
    {
        // Outside a request there is no shell scope to defer to: the handlers must not throw.
        await new GarnishmentOrderWorkflowHandler().CreatedAsync(new CreatedContext<GarnishmentOrder>(new GarnishmentOrder { ItemId = "o1" }), TestContext.Current.CancellationToken);
        await new TaxDepositWorkflowHandler().CreatedAsync(new CreatedContext<TaxDeposit>(new TaxDeposit { ItemId = "d1" }), TestContext.Current.CancellationToken);
    }
}

/// <summary>
/// Records what the filing service tells its observers.
/// </summary>
internal sealed class RecordingFilingObserver : IPayrollFilingObserver
{
    public List<PayrollFilingChange> Changes { get; } = [];

    public Task ChangedAsync(PayrollFilingChange change, CancellationToken cancellationToken = default)
    {
        Changes.Add(change);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Records what garnishments tell their observers.
/// </summary>
internal sealed class RecordingGarnishmentObserver : IGarnishmentObserver
{
    public List<GarnishmentChange> Changes { get; } = [];

    public Task ChangedAsync(GarnishmentChange change, CancellationToken cancellationToken = default)
    {
        Changes.Add(change);

        return Task.CompletedTask;
    }
}
