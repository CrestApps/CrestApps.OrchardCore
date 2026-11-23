using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Localization;
using OrchardCore.Modules;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Tests.Taxation;

/// <summary>
/// Tax Returns and E-Filing: the periods and due dates of a filing schedule, a return prepared from a data provider and
/// worked out by its form, its life (ready, filed, paid, amended), a late draft worked out as filed today, and filing it
/// by hand through the manual channel. Today is 2 November 2026 (the harness's clock); the registration files monthly,
/// due the 20th of the month after, with an annual return due 28 February.
/// </summary>
public sealed class TaxReturnsTests
{
    private const string RegistrationId = "4tr00000000000000000000001";

    [Theory]
    [InlineData("Monthly", "2026-10-15", 1, 20, "2026-11-20")]
    [InlineData("Quarterly", "2026-08-01", 1, 20, "2026-10-20")]
    [InlineData("Monthly", "2026-05-01", 1, 20, "2026-06-22")]
    [InlineData("Monthly", "2026-03-31", 1, 31, "2026-04-30")]
    [InlineData("Annual", "2026-06-30", 2, 28, "2027-03-01")]
    public void DueDate_IsTheDayOfTheMonthAfter_MovedOffTheWeekend(string frequency, string day, int monthsAfter, int dueDay, string expected)
    {
        // Arrange
        var schedule = new TaxFilingSchedule
        {
            Frequency = frequency,
            DueMonthsAfterPeriod = monthsAfter,
            DueDay = dueDay,
        };

        // Act
        var due = schedule.DueDateOf(schedule.PeriodOf(DateOnly.Parse(day, CultureInfo.InvariantCulture)));

        // Assert
        Assert.Equal(DateOnly.Parse(expected, CultureInfo.InvariantCulture), due);
    }

    [Fact]
    public void DueDate_StaysOnTheWeekend_WhenTheScheduleDoesNotMoveIt()
    {
        // Arrange
        var schedule = new TaxFilingSchedule
        {
            Frequency = TaxFilingFrequencies.Monthly,
            MoveToNextBusinessDay = false,
        };

        // Act
        var due = schedule.DueDateOf(TaxReturnPeriod.For(TaxFilingFrequencies.Monthly, new DateOnly(2026, 5, 10)));

        // Assert
        Assert.Equal(new DateOnly(2026, 6, 20), due);
    }

    [Theory]
    [InlineData("2026-10", "Monthly", "2026-10-01", "2026-10-31")]
    [InlineData("2026-Q3", "Quarterly", "2026-07-01", "2026-09-30")]
    [InlineData("2026", "Annual", "2026-01-01", "2026-12-31")]
    public void PeriodKey_ReadsBackIntoItsPeriod(string key, string frequency, string start, string end)
    {
        // Act
        var period = TaxReturnPeriod.Parse(key);

        // Assert
        Assert.Equal(new TaxReturnPeriod(frequency, DateOnly.Parse(start, CultureInfo.InvariantCulture), DateOnly.Parse(end, CultureInfo.InvariantCulture)), period);
        Assert.Equal(key, period.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2026-13")]
    [InlineData("2026-Q5")]
    [InlineData("26-10")]
    public void PeriodKey_RefusesWhatIsNotAPeriod(string key)
        => Assert.Null(TaxReturnPeriod.Parse(key));

    [Fact]
    public void Periods_AreTheOnesEndingInTheSpan()
    {
        // Act
        var quarters = TaxReturnPeriod.Between(TaxFilingFrequencies.Quarterly, new DateOnly(2026, 2, 15), new DateOnly(2026, 12, 31));

        // Assert
        Assert.Equal(["2026-Q1", "2026-Q2", "2026-Q3", "2026-Q4"], quarters.Select(period => period.Key));
    }

    [Fact]
    public async Task GenericForm_SumsTheLinesByKind()
    {
        // Arrange
        var taxReturn = new TaxReturn
        {
            Lines =
            [
                new TaxReturnLine { Kind = TaxReturnLineKinds.GrossSales, Amount = 10000m },
                new TaxReturnLine { Kind = TaxReturnLineKinds.ExemptSales, Amount = 2000m, Category = "Resale" },
                new TaxReturnLine { Kind = TaxReturnLineKinds.TaxableSales, Amount = 8000m, TaxAmount = 480m, TaxType = TaxTypeNames.SalesTax },
                new TaxReturnLine { Kind = TaxReturnLineKinds.UseTaxPurchases, Amount = 500m, TaxAmount = 30m, TaxType = TaxTypeNames.UseTax },
                new TaxReturnLine { Kind = TaxReturnLineKinds.Withholding, TaxAmount = 120m },
                new TaxReturnLine { Kind = TaxReturnLineKinds.Credit, TaxAmount = 10m },
            ],
        };

        // Act
        await new GenericTaxReturnForm().ComputeAsync(new TaxReturnFormContext
        {
            Registration = new MerchantTaxRegistration(),
            Return = taxReturn,
            Services = new ServiceCollection().BuildServiceProvider(),
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((10000m, 2000m, 8000m, 480m, 30m, 120m), (taxReturn.Boxes["GrossSales"], taxReturn.Boxes["ExemptSales"], taxReturn.Boxes["TaxableSales"], taxReturn.Boxes["SalesTax"], taxReturn.Boxes["UseTax"], taxReturn.Boxes["Withholding"]));
        Assert.Equal((630m, 10m, 620m), (taxReturn.Boxes["TotalTax"], taxReturn.Boxes["Credits"], taxReturn.AmountDue));
    }

    [Fact]
    public async Task Return_IsPreparedFiledPaidAndAmended()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var user = CalendarTestHarness.User("filer", TaxationPermissions.FileTaxReturns.Name);
        var observer = harness.Services.GetRequiredService<RecordingObserver>();
        var data = harness.Services.GetRequiredService<FixedDataProvider>();

        // Act
        var periods = await harness.RunAsync(services => services.GetRequiredService<ITaxReturnService>().ListPeriodsAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)), commit: false);

        var prepared = await harness.RunAsync(services => services.GetRequiredService<ITaxReturnService>().PrepareAsync(RegistrationId, "2026-10", annualReconciliation: false, user));

        data.TaxCollected = 66m;

        var again = await harness.RunAsync(services => services.GetRequiredService<ITaxReturnService>().PrepareAsync(RegistrationId, "2026-10", annualReconciliation: false, user));

        var filed = await harness.RunAsync(async services =>
        {
            var service = services.GetRequiredService<ITaxReturnService>();
            var taxReturn = await services.GetRequiredService<ITaxReturnManager>().FindByIdAsync(prepared.Return.ItemId);

            Assert.True((await service.MarkReadyAsync(taxReturn, user)).Succeeded);
            Assert.False((await service.PrepareAsync(RegistrationId, "2026-10", annualReconciliation: false, user)).Succeeded);

            var result = await service.MarkFiledAsync(taxReturn, new DateOnly(2026, 11, 18), " MI-123 ", channel: null, user);

            Assert.True(result.Succeeded);
            Assert.True((await service.RecordPaymentAsync(taxReturn, new TaxReturnPayment { Amount = 69m, PaidOn = new DateOnly(2026, 11, 18), Reference = "ACH" }, user)).Succeeded);

            return taxReturn;
        });

        data.TaxCollected = 100m;

        var amendment = await harness.RunAsync(async services =>
        {
            var service = services.GetRequiredService<ITaxReturnService>();
            var taxReturn = await services.GetRequiredService<ITaxReturnManager>().FindByIdAsync(prepared.Return.ItemId);
            var result = await service.AmendAsync(taxReturn, user);

            Assert.True(result.Succeeded);
            Assert.True((await service.MarkFiledAsync(result.Return, new DateOnly(2026, 11, 19), "MI-124", channel: null, user)).Succeeded);

            return result.Return;
        });

        var original = await harness.RunAsync(services => services.GetRequiredService<ITaxReturnManager>().FindByIdAsync(prepared.Return.ItemId).AsTask(), commit: false);

        // Assert
        // Twelve months and the annual return, by due date: the annual one, due 1 March 2027 (28 February is a Sunday), last.
        Assert.Equal(13, periods.Count);
        Assert.Equal(("2026-01", new DateOnly(2026, 2, 20)), (periods[0].Period.Key, periods[0].DueDate));
        Assert.True(periods[^1].IsAnnualReconciliation);
        Assert.Equal(("2026", "US-MI-ANNUAL", new DateOnly(2027, 3, 1)), (periods[^1].Period.Key, periods[^1].Form, periods[^1].DueDate));

        // The draft: 1000 of taxable sales with 60 of tax collected and 3 of use tax; prepared again with 66 collected.
        Assert.True(prepared.Succeeded);
        Assert.Equal((TaxReturnStatus.Draft, new DateOnly(2026, 11, 20), 63m), (prepared.Return.Status, prepared.Return.DueDate, prepared.Return.AmountDue));
        Assert.Equal(nameof(FixedDataProvider), prepared.Return.Lines[0].Source);
        Assert.Equal(prepared.Return.ItemId, again.Return.ItemId);
        Assert.Equal(69m, again.Return.AmountDue);

        // Filed on time with its confirmation, and paid in full.
        Assert.Equal((TaxReturnStatus.Filed, new DateOnly(2026, 11, 18), "MI-123", 0m), (filed.Status, filed.FiledOn, filed.ConfirmationNumber, filed.Balance));

        // The amendment counts the 69 paid with the original, so 34 more is owed; the original is amended.
        Assert.Equal((1, prepared.Return.ItemId, 69m, 103m, 34m), (amendment.Amendment, amendment.AmendsId, amendment.PaidWithEarlier, amendment.AmountDue, amendment.Balance));
        Assert.Equal(TaxReturnStatus.Amended, original.Status);

        Assert.Equal(
            [TaxReturnChangeKind.Prepared, TaxReturnChangeKind.Prepared, TaxReturnChangeKind.Ready, TaxReturnChangeKind.Filed, TaxReturnChangeKind.PaymentRecorded, TaxReturnChangeKind.Amended, TaxReturnChangeKind.Filed],
            observer.Changes.Select(change => change.Kind));
        Assert.Equal(69m, observer.Changes[4].Payment.Amount);
        Assert.Equal("filer", observer.Changes[4].Actor);
    }

    [Fact]
    public async Task LateDraft_IsWorkedOutAsFiledToday_AndAFiledReturnAsFiledThatDay()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var user = CalendarTestHarness.User("filer", TaxationPermissions.FileTaxReturns.Name);
        var form = harness.Services.GetRequiredService<RecordingForm>();

        // Act: September was due 20 October; today is 2 November (New York).
        var draft = await harness.RunAsync(services => services.GetRequiredService<ITaxReturnService>().PrepareAsync(RegistrationId, "2026-09", annualReconciliation: false, user));
        var preparedAsOf = form.FiledOn;

        await harness.RunAsync(async services =>
        {
            var taxReturn = await services.GetRequiredService<ITaxReturnManager>().FindByIdAsync(draft.Return.ItemId);

            await services.GetRequiredService<ITaxReturnService>().MarkFiledAsync(taxReturn, new DateOnly(2026, 10, 30), null, null, user);
        });

        // Assert
        Assert.Equal(new DateOnly(2026, 11, 2), preparedAsOf);
        Assert.Equal(new DateOnly(2026, 10, 30), form.FiledOn);
    }

    [Fact]
    public async Task ManualChannel_HandsOverTheFile_AndTheRecordedAcceptanceFilesTheReturn()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var user = CalendarTestHarness.User("filer", TaxationPermissions.FileTaxReturns.Name);
        var prepared = await harness.RunAsync(services => services.GetRequiredService<ITaxReturnService>().PrepareAsync(RegistrationId, "2026-10", annualReconciliation: false, user));

        // Act
        var (draftReason, submitted, json) = await harness.RunAsync(async services =>
        {
            var eFiling = services.GetRequiredService<IEFilingService>();
            var draftTarget = await eFiling.FindTargetAsync(TaxationConstants.TaxReturnEFileTargetType, prepared.Return.ItemId);
            var taxReturn = await services.GetRequiredService<ITaxReturnManager>().FindByIdAsync(prepared.Return.ItemId);

            await services.GetRequiredService<ITaxReturnService>().MarkReadyAsync(taxReturn, user);

            var target = await eFiling.FindTargetAsync(TaxationConstants.TaxReturnEFileTargetType, prepared.Return.ItemId);
            var result = await eFiling.SubmitAsync(target, TaxReturnJsonEFileFormat.FormatName, ManualEFileTransport.TransportName, user);

            Assert.True(result.Succeeded, string.Join(" ", result.Errors));

            return (draftTarget.NotFileableReason, result.Submission, Encoding.UTF8.GetString(Convert.FromBase64String(result.Submission.Content)));
        });

        var (accepted, again) = await harness.RunAsync(async services =>
        {
            var eFiling = services.GetRequiredService<IEFilingService>();
            var submission = await services.GetRequiredService<IEFileSubmissionManager>().FindByIdAsync(submitted.ItemId);
            var answer = await eFiling.RecordAnswerAsync(submission, accepted: true, "MI-555", new DateOnly(2026, 11, 3), message: null, user);
            var target = await eFiling.FindTargetAsync(TaxationConstants.TaxReturnEFileTargetType, prepared.Return.ItemId);

            return (answer, await eFiling.SubmitAsync(target, TaxReturnJsonEFileFormat.FormatName, ManualEFileTransport.TransportName, user));
        });

        var filed = await harness.RunAsync(services => services.GetRequiredService<ITaxReturnManager>().FindByIdAsync(prepared.Return.ItemId).AsTask(), commit: false);

        // Assert
        Assert.Equal("Mark the return ready to file first.", draftReason);
        Assert.Equal((EFileSubmissionStatus.Submitted, true, "Generic-2026-10.json"), (submitted.Status, submitted.IsManual, submitted.FileName));
        Assert.Contains("\"RegistrationNumber\": \"MI-0001\"", json, StringComparison.Ordinal);
        Assert.Contains("\"AmountDue\": 63", json, StringComparison.Ordinal);
        Assert.True(accepted.Succeeded, string.Join(" ", accepted.Errors));
        Assert.Equal((EFileSubmissionStatus.Accepted, "MI-555"), (accepted.Submission.Status, accepted.Submission.ConfirmationNumber));
        Assert.Equal((TaxReturnStatus.Filed, new DateOnly(2026, 11, 3), "MI-555", ManualEFileTransport.TransportName), (filed.Status, filed.FiledOn, filed.ConfirmationNumber, filed.FilingChannel));
        Assert.False(again.Succeeded);
    }

    [Fact]
    public async Task JsonFormat_RefusesAReturnWithoutARegistrationNumber()
    {
        // Arrange
        var format = new TaxReturnJsonEFileFormat(new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<TaxReturnJsonEFileFormat>());
        var target = new EFileTarget
        {
            Type = TaxationConstants.TaxReturnEFileTargetType,
            Id = "r1",
            Document = new TaxReturn { Form = "Generic", PeriodKey = "2026-10" },
        };

        // Act
        var payload = await format.WriteAsync(target, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["The registration has no registration number: add the number the agency gave."], payload.Problems);
    }

    private static Task<CalendarTestHarness> CreateHarnessAsync()
        => CalendarTestHarness.CreateAsync(
            services =>
            {
                var registrations = new InMemoryMerchantTaxRegistrationStore();

                registrations.CreateAsync(new MerchantTaxRegistration
                {
                    ItemId = RegistrationId,
                    Name = "Michigan sales, use and withholding",
                    JurisdictionId = "us-mi",
                    RegistrationNumber = "MI-0001",
                    Filing = new TaxFilingSchedule
                    {
                        Frequency = TaxFilingFrequencies.Monthly,
                        AnnualForm = "US-MI-ANNUAL",
                        AnnualDueMonthsAfterYear = 2,
                        AnnualDueDay = 28,
                    },
                }).AsTask().GetAwaiter().GetResult();

                services.AddSingleton<IMerchantTaxRegistrationStore>(registrations);
                services.AddSingleton<ILocalClock>(new FixedOffsetLocalClock(() => CalendarTestHarness.Now, TimeSpan.FromHours(-5)));
                services.AddTaxReturnsCore();
                services.AddEFilingCore();
                services.AddScoped<ICatalogEntryHandler<TaxReturn>, TaxReturnHandler>();
                services.AddScoped<ICatalogEntryHandler<EFileSubmission>, EFileSubmissionHandler>();
                services.AddSingleton<FixedDataProvider>();
                services.AddSingleton<ITaxReturnDataProvider>(provider => provider.GetRequiredService<FixedDataProvider>());
                services.AddSingleton<RecordingObserver>();
                services.AddSingleton<ITaxReturnObserver>(provider => provider.GetRequiredService<RecordingObserver>());
                services.AddSingleton<RecordingForm>();
                services.AddSingleton<ITaxReturnForm>(provider => provider.GetRequiredService<RecordingForm>());
                services.AddSingleton<ITaxReturnForm>(new AnnualForm());
            },
            async schemaBuilder =>
            {
                await TaxReturnIndexSchema.CreateAsync(schemaBuilder);
                await EFileSubmissionIndexSchema.CreateAsync(schemaBuilder);
            },
            indexProviders: [new TaxReturnIndexProvider(), new EFileSubmissionIndexProvider()],
            collections: [TaxationConstants.TaxReturnsCollectionName]);

    private sealed class FixedDataProvider : ITaxReturnDataProvider
    {
        public decimal TaxCollected { get; set; } = 60m;

        public string Name
            => nameof(FixedDataProvider);

        public Task ContributeAsync(TaxReturnDataContext context, CancellationToken cancellationToken = default)
        {
            context.Return.Currency = "USD";
            context.Lines.Add(new TaxReturnLine { Kind = TaxReturnLineKinds.TaxableSales, TaxType = TaxTypeNames.SalesTax, Amount = 1000m, TaxAmount = TaxCollected });
            context.Lines.Add(new TaxReturnLine { Kind = TaxReturnLineKinds.UseTaxPurchases, TaxType = TaxTypeNames.UseTax, Amount = 50m, TaxAmount = 3m, Source = "Purchases" });

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingObserver : ITaxReturnObserver
    {
        public List<TaxReturnChange> Changes { get; } = [];

        public Task ChangedAsync(TaxReturnChange change, CancellationToken cancellationToken = default)
        {
            Changes.Add(new TaxReturnChange
            {
                Kind = change.Kind,
                Return = change.Return,
                Payment = change.Payment,
                Actor = change.Actor,
            });

            return Task.CompletedTask;
        }
    }

    // Replaces the generic form (same name, registered after it) and remembers the day it was worked out as filed on.
    private sealed class RecordingForm : ITaxReturnForm
    {
        private readonly GenericTaxReturnForm _generic = new();

        public DateOnly? FiledOn { get; private set; }

        public string Name
            => GenericTaxReturnForm.FormName;

        [JsonIgnore]
        public LocalizationSource DisplayName
            => _generic.DisplayName;

        public Task ComputeAsync(TaxReturnFormContext context, CancellationToken cancellationToken = default)
        {
            FiledOn = context.FiledOn;

            return _generic.ComputeAsync(context, cancellationToken);
        }
    }

    private sealed class AnnualForm : ITaxReturnForm
    {
        public string Name
            => "US-MI-ANNUAL";

        [JsonIgnore]
        public LocalizationSource DisplayName
            => LocalizationSource.Create<AnnualForm>("Annual return");

        public Task ComputeAsync(TaxReturnFormContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
