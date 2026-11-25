using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Core.TaxForms;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;
using OrchardCore.Modules;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Tests.Taxation;

/// <summary>
/// Tax Returns and E-Filing: the periods and due dates of a filing schedule, a return prepared from a data provider and
/// worked out by its form (the generic return the Tax Returns recipe adds as a tax form), its life (ready, filed, paid,
/// amended), a late draft worked out as filed today, and filing it by hand through the manual channel. Today is 2 November 2026 (the harness's clock); the registration files monthly,
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
    public void GenericForm_SumsTheLinesByKind()
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
        SalesTaxFormRecipes.Compute(SalesTaxFormRecipes.GenericForm(), taxReturn);

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
        // Arrange: the generic form gets a field of the days late, as the Tax Forms page adds one.
        await using var harness = await CreateHarnessAsync();
        var user = CalendarTestHarness.User("filer", TaxationPermissions.FileTaxReturns.Name);

        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ITaxFormManager>();
            var generic = await manager.FindByNameAsync(TaxationConstants.GenericTaxReturnFormName);

            generic.Fields.Add(new TaxFormField { Number = "DaysLate", Label = "Days late", Level = TaxFormFieldLevel.Filing, Formula = "DAYSLATE()" });
            Assert.True((await manager.ValidateAsync(generic)).Succeeded);
            await manager.UpdateAsync(generic);
        });

        // Act: September was due 20 October; today is 2 November (New York).
        var draft = await harness.RunAsync(services => services.GetRequiredService<ITaxReturnService>().PrepareAsync(RegistrationId, "2026-09", annualReconciliation: false, user));

        var filed = await harness.RunAsync(async services =>
        {
            var taxReturn = await services.GetRequiredService<ITaxReturnManager>().FindByIdAsync(draft.Return.ItemId);

            await services.GetRequiredService<ITaxReturnService>().MarkFiledAsync(taxReturn, new DateOnly(2026, 10, 30), null, null, user);

            return taxReturn;
        });

        // Assert: 13 days late as of today; 10 days as filed on 30 October.
        Assert.Equal(13m, draft.Return.Boxes["DaysLate"]);
        Assert.Equal(10m, filed.Boxes["DaysLate"]);
    }

    [Fact]
    public async Task EditorBuiltForm_IsTheRegistrationsReturnForm_AndItsAmountDueField_IsTheAmountDue()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var user = CalendarTestHarness.User("filer", TaxationPermissions.FileTaxReturns.Name);

        var (refused, noAmountDue) = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ITaxFormManager>();

            // A sales tax form has no recipients, and its amount due is one of its filing fields.
            var withRecipient = SalesTaxFormRecipes.MichiganStyleForm();

            withRecipient.Name = "US-MI-TEST-2";
            withRecipient.Fields.Add(new TaxFormField { Number = "P", Level = TaxFormFieldLevel.Recipient, Formula = "1" });

            var withoutDue = SalesTaxFormRecipes.MichiganStyleForm();

            withoutDue.Name = "US-MI-TEST-3";
            withoutDue.Fields = [.. withoutDue.Fields.Where(field => field.Number != "21")];

            var form = SalesTaxFormRecipes.MichiganStyleForm();

            Assert.True((await manager.ValidateAsync(form)).Succeeded);
            await manager.CreateAsync(form);

            return ((await manager.ValidateAsync(withRecipient)).Succeeded, (await manager.ValidateAsync(withoutDue)).Succeeded);
        });

        var registrations = harness.Services.GetRequiredService<IMerchantTaxRegistrationStore>();

        var registration = await registrations.FindByIdAsync(RegistrationId, TestContext.Current.CancellationToken);

        registration.Filing.Form = "US-MI-TEST";
        await registrations.UpdateAsync(registration, TestContext.Current.CancellationToken);

        // Act
        var forms = await harness.RunAsync(services => services.GetRequiredService<ITaxReturnService>().GetFormsAsync(), commit: false);
        var prepared = await harness.RunAsync(services => services.GetRequiredService<ITaxReturnService>().PrepareAsync(RegistrationId, "2026-10", annualReconciliation: false, user));

        // Assert: the tax forms and the form a module adds in code are offered.
        Assert.False(refused);
        Assert.False(noAmountDue);
        Assert.Contains("US-MI-TEST", forms.Keys);
        Assert.True(forms.ContainsKey(TaxationConstants.GenericTaxReturnFormName));
        Assert.IsType<AnnualForm>(forms["US-MI-ANNUAL"]);

        // 60 of sales tax and 3 of use tax, filed on time (due 20 November): a discount of 0.32.
        Assert.True(prepared.Succeeded);
        Assert.Equal("US-MI-TEST", prepared.Return.Form);
        Assert.Equal((60m, 3m, 63m, 0.32m), (prepared.Return.Boxes["4"], prepared.Return.Boxes["5"], prepared.Return.Boxes["6"], prepared.Return.Boxes["7"]));
        Assert.Equal(62.68m, prepared.Return.AmountDue);
        Assert.Contains("The registration number is not a Michigan account number.", prepared.Return.Notes);
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
    public async Task MarkFiled_RefusesADayBeforeThePeriodEnds_AndTakesItsLastDay()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var user = CalendarTestHarness.User("filer", TaxationPermissions.FileTaxReturns.Name);
        var prepared = await harness.RunAsync(services => services.GetRequiredService<ITaxReturnService>().PrepareAsync(RegistrationId, "2026-10", annualReconciliation: false, user));

        // Act
        var (early, onLastDay) = await harness.RunAsync(async services =>
        {
            var service = services.GetRequiredService<ITaxReturnService>();
            var taxReturn = await services.GetRequiredService<ITaxReturnManager>().FindByIdAsync(prepared.Return.ItemId);
            var refused = await service.MarkFiledAsync(taxReturn, new DateOnly(2026, 10, 15), null, null, user);
            var filed = await service.MarkFiledAsync(taxReturn, new DateOnly(2026, 10, 31), null, null, user);

            return (refused, filed);
        });

        // Assert
        Assert.False(early.Succeeded);
        Assert.StartsWith("A return is filed once its period is over", Assert.Single(early.Errors), StringComparison.Ordinal);
        Assert.True(onLastDay.Succeeded, string.Join(" ", onLastDay.Errors));
        Assert.Equal((TaxReturnStatus.Filed, new DateOnly(2026, 10, 31)), (onLastDay.Return.Status, onLastDay.Return.FiledOn));
    }

    [Fact]
    public async Task EFiling_ARunningPeriod_IsNotFileableYet()
    {
        // Arrange: today is 2 November, so November is still running.
        await using var harness = await CreateHarnessAsync();
        var user = CalendarTestHarness.User("filer", TaxationPermissions.FileTaxReturns.Name);
        var prepared = await harness.RunAsync(services => services.GetRequiredService<ITaxReturnService>().PrepareAsync(RegistrationId, "2026-11", annualReconciliation: false, user));

        // Act
        var reason = await harness.RunAsync(async services =>
        {
            var taxReturn = await services.GetRequiredService<ITaxReturnManager>().FindByIdAsync(prepared.Return.ItemId);

            await services.GetRequiredService<ITaxReturnService>().MarkReadyAsync(taxReturn, user);

            return (await services.GetRequiredService<IEFilingService>().FindTargetAsync(TaxationConstants.TaxReturnEFileTargetType, prepared.Return.ItemId)).NotFileableReason;
        });

        // Assert
        Assert.StartsWith("The period ends on", reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestSubmission_IsAcceptedWithoutFilingTheReturn_AndCarriesTheChoicesToTheFormatAndChannel()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync(AddGateway);
        var user = CalendarTestHarness.User("filer", TaxationPermissions.FileTaxReturns.Name);
        var gateway = harness.Services.GetRequiredService<GatewayTransport>();
        var format = harness.Services.GetRequiredService<ChoicesFormat>();
        var prepared = await PrepareReadyAsync(harness, user);

        // Act
        var test = await harness.RunAsync(async services =>
        {
            var eFiling = services.GetRequiredService<IEFilingService>();
            var request = new EFileSubmissionRequest
            {
                Target = await eFiling.FindTargetAsync(TaxationConstants.TaxReturnEFileTargetType, prepared),
                Format = ChoicesFormat.FormatName,
                Transport = GatewayTransport.TransportName,
                IsTest = true,
            };

            request.Options["DebitPayment"] = "true";
            request.Options["DebitOn"] = " 2026-11-20 ";

            return await eFiling.SubmitAsync(request, user);
        });

        var afterTest = await harness.RunAsync(services => services.GetRequiredService<ITaxReturnManager>().FindByIdAsync(prepared).AsTask(), commit: false);

        var real = await harness.RunAsync(async services =>
        {
            var eFiling = services.GetRequiredService<IEFilingService>();
            var target = await eFiling.FindTargetAsync(TaxationConstants.TaxReturnEFileTargetType, prepared);

            return await eFiling.SubmitAsync(target, ChoicesFormat.FormatName, GatewayTransport.TransportName, user);
        });

        var filed = await harness.RunAsync(services => services.GetRequiredService<ITaxReturnManager>().FindByIdAsync(prepared).AsTask(), commit: false);

        // Assert
        Assert.True(test.Succeeded, string.Join(" ", test.Errors));
        Assert.Equal((EFileSubmissionStatus.Accepted, true, "GW-1"), (test.Submission.Status, test.Submission.IsTest, test.Submission.ConfirmationNumber));
        Assert.Equal("2026-11-20", test.Submission.Options["DebitOn"]);
        Assert.Equal(("true", true), (format.Written[0].Options["DebitPayment"], format.Written[0].IsTest));
        Assert.True(gateway.Sent[0].IsTest);
        Assert.Equal(TaxReturnStatus.Ready, afterTest.Status);
        Assert.True(real.Succeeded, string.Join(" ", real.Errors));
        Assert.False(real.Submission.IsTest);
        Assert.Empty(real.Submission.Options);
        Assert.Equal((TaxReturnStatus.Filed, "GW-1"), (filed.Status, filed.ConfirmationNumber));
    }

    [Fact]
    public async Task TestSubmission_IsRefusedByAChannelThatTakesNoTests()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync();
        var user = CalendarTestHarness.User("filer", TaxationPermissions.FileTaxReturns.Name);
        var prepared = await PrepareReadyAsync(harness, user);

        // Act
        var result = await harness.RunAsync(async services =>
        {
            var eFiling = services.GetRequiredService<IEFilingService>();

            return await eFiling.SubmitAsync(new EFileSubmissionRequest
            {
                Target = await eFiling.FindTargetAsync(TaxationConstants.TaxReturnEFileTargetType, prepared),
                Format = TaxReturnJsonEFileFormat.FormatName,
                Transport = ManualEFileTransport.TransportName,
                IsTest = true,
            }, user);
        });

        // Assert
        Assert.Equal(["The filing channel chosen does not take test submissions."], result.Errors);
        Assert.Null(result.Submission);
    }

    [Fact]
    public async Task ChannelSetUpForTesting_RecordsTheSubmissionAsATest_EvenWhenNoTestWasAsked()
    {
        // Arrange
        await using var harness = await CreateHarnessAsync(AddGateway);
        var user = CalendarTestHarness.User("filer", TaxationPermissions.FileTaxReturns.Name);
        harness.Services.GetRequiredService<GatewayTransport>().AnswersAsTest = true;
        var prepared = await PrepareReadyAsync(harness, user);

        // Act
        var result = await harness.RunAsync(async services =>
        {
            var eFiling = services.GetRequiredService<IEFilingService>();
            var target = await eFiling.FindTargetAsync(TaxationConstants.TaxReturnEFileTargetType, prepared);

            return await eFiling.SubmitAsync(target, ChoicesFormat.FormatName, GatewayTransport.TransportName, user);
        });

        var taxReturn = await harness.RunAsync(services => services.GetRequiredService<ITaxReturnManager>().FindByIdAsync(prepared).AsTask(), commit: false);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal((EFileSubmissionStatus.Accepted, true), (result.Submission.Status, result.Submission.IsTest));
        Assert.Equal(TaxReturnStatus.Ready, taxReturn.Status);
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
        var payload = await format.WriteAsync(target, new EFileSubmission(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["The registration has no registration number: add the number the agency gave."], payload.Problems);
    }

    // Prepares October and marks it ready to file; returns its id.
    private static async Task<string> PrepareReadyAsync(CalendarTestHarness harness, ClaimsPrincipal user)
    {
        var prepared = await harness.RunAsync(services => services.GetRequiredService<ITaxReturnService>().PrepareAsync(RegistrationId, "2026-10", annualReconciliation: false, user));

        await harness.RunAsync(async services =>
        {
            var taxReturn = await services.GetRequiredService<ITaxReturnManager>().FindByIdAsync(prepared.Return.ItemId);

            Assert.True((await services.GetRequiredService<ITaxReturnService>().MarkReadyAsync(taxReturn, user)).Succeeded);
        });

        return prepared.Return.ItemId;
    }

    private static void AddGateway(IServiceCollection services)
    {
        services.AddSingleton<GatewayTransport>();
        services.AddSingleton<IEFileTransport>(provider => provider.GetRequiredService<GatewayTransport>());
        services.AddSingleton<ChoicesFormat>();
        services.AddSingleton<IEFileFormat>(provider => provider.GetRequiredService<ChoicesFormat>());
    }

    private static async Task<CalendarTestHarness> CreateHarnessAsync(Action<IServiceCollection> configure = null)
    {
        var harness = await CalendarTestHarness.CreateAsync(
            services =>
            {
                configure?.Invoke(services);

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
                services.AddSingleton<ITaxReturnForm>(new AnnualForm());
                services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TaxForm>, TaxFormHandler>());
                services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TaxForm>, SalesTaxFormHandler>());
            },
            async schemaBuilder =>
            {
                await TaxReturnIndexSchema.CreateAsync(schemaBuilder);
                await EFileSubmissionIndexSchema.CreateAsync(schemaBuilder);
                await TaxFormIndexSchema.CreateAsync(schemaBuilder);
            },
            indexProviders: [new TaxReturnIndexProvider(), new EFileSubmissionIndexProvider(), new TaxFormIndexProvider()],
            collections: [TaxationConstants.TaxReturnsCollectionName, TaxationConstants.TaxFormsCollectionName]);

        // The generic return, as Tax Returns' migration adds it from its recipe.
        await SalesTaxFormRecipes.SeedAsync(harness);

        return harness;
    }

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

    // A state gateway that answers at once: accepted, or, set up for acceptance testing, accepted as a test.
    private sealed class GatewayTransport : IEFileTransport
    {
        public const string TransportName = "TestGateway";

        public List<EFileSubmission> Sent { get; } = [];

        public bool AnswersAsTest { get; set; }

        public string Name
            => TransportName;

        [JsonIgnore]
        public LocalizationSource DisplayName
            => LocalizationSource.Create<GatewayTransport>("State gateway");

        public bool IsManual
            => false;

        public bool SupportsTestSubmissions
            => true;

        public bool CanSend(EFileTarget target, string format)
            => true;

        public Task<EFileTransportResult> SendAsync(EFileSubmission submission, EFilePayload payload, CancellationToken cancellationToken = default)
        {
            Sent.Add(submission);

            return Task.FromResult(new EFileTransportResult
            {
                Status = EFileSubmissionStatus.Accepted,
                ConfirmationNumber = "GW-1",
                FiledOn = new DateOnly(2026, 11, 2),
                IsTest = AnswersAsTest,
            });
        }

        public Task<EFileTransportResult> CheckAsync(EFileSubmission submission, CancellationToken cancellationToken = default)
            => Task.FromResult(new EFileTransportResult { Status = EFileSubmissionStatus.Submitted });
    }

    // Remembers the choices and the test flag of each submission it writes.
    private sealed class ChoicesFormat : IEFileFormat
    {
        public const string FormatName = "Choices";

        public List<(Dictionary<string, string> Options, bool IsTest)> Written { get; } = [];

        public string Name
            => FormatName;

        [JsonIgnore]
        public LocalizationSource DisplayName
            => LocalizationSource.Create<ChoicesFormat>("Choices");

        public bool CanWrite(EFileTarget target)
            => target?.Document is TaxReturn;

        public Task<EFilePayload> WriteAsync(EFileTarget target, EFileSubmission submission, CancellationToken cancellationToken = default)
        {
            Written.Add((new Dictionary<string, string>(submission.Options), submission.IsTest));

            return Task.FromResult(new EFilePayload
            {
                FileName = "return.txt",
                ContentType = "text/plain",
                Content = Encoding.UTF8.GetBytes("return"),
            });
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
