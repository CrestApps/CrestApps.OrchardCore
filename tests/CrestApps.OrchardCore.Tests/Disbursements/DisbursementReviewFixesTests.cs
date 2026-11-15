using CrestApps.Core;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Disbursements.Core.Nacha;
using CrestApps.OrchardCore.Disbursements.Models;
using CrestApps.OrchardCore.Disbursements.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.SourceAccess;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Disbursements;

/// <summary>
/// The review of Disbursements: one disbursement per key under concurrency, a key that asks for something else or
/// after a failure refused, one release per batch with a Releasing state before the provider is called, returns and
/// notices limited to what the person may act on, settling counted from the arrival day, a cancelled batch reported as
/// unbatched, and the bank file's entry description, account length and arrival date checked.
/// </summary>
public sealed class DisbursementReviewFixesTests
{
    [Fact]
    public async Task CreateAsync_TwoAtOnceWithOneKey_CreatesOnce()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        context.Store.FindDelay = TimeSpan.FromMilliseconds(50);

        // Act
        var results = await Task.WhenAll(
            Task.Run(() => context.Service.CreateAsync(Manual("k-1", 10m), TestContext.Current.CancellationToken)),
            Task.Run(() => context.Service.CreateAsync(Manual("k-1", 10m), TestContext.Current.CancellationToken)));

        // Assert
        Assert.Single(context.Store.Entries);
        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Single(results, result => result.AlreadyExisted);
        Assert.Equal(results[0].Disbursement.ItemId, results[1].Disbursement.ItemId);
    }

    [Fact]
    public async Task CreateAsync_AKeyUsedForAnotherPayment_IsRefused()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        await context.Service.CreateAsync(Manual("k-2", 10m), TestContext.Current.CancellationToken);

        // Act
        var otherAmount = await context.Service.CreateAsync(Manual("k-2", 12m), TestContext.Current.CancellationToken);
        var otherPayee = await context.Service.CreateAsync(Manual("k-2", 10m, "Lee"), TestContext.Current.CancellationToken);
        var same = await context.Service.CreateAsync(Manual("k-2", 10m), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(otherAmount.Succeeded);
        Assert.False(otherPayee.Succeeded);
        Assert.True(same.Succeeded);
        Assert.True(same.AlreadyExisted);
        Assert.Single(context.Store.Entries);
    }

    [Fact]
    public async Task CreateAsync_AfterTheKeysDisbursementFailed_SaysANewAttemptNeedsANewKey()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var first = (await context.Service.CreateAsync(Manual("k-3", 10m), TestContext.Current.CancellationToken)).Disbursement;
        await context.Service.CancelAsync(first, "Wrong", TestContext.Current.CancellationToken);

        // Act
        var retry = await context.Service.CreateAsync(Manual("k-3", 10m), TestContext.Current.CancellationToken);
        var newKey = await context.Service.CreateAsync(Manual("k-3-b", 10m), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(retry.Succeeded);
        Assert.Contains("new key", Assert.Single(retry.Errors), StringComparison.Ordinal);
        Assert.True(newKey.Succeeded);
        Assert.False(newKey.AlreadyExisted);
    }

    [Fact]
    public async Task ReleaseBatchAsync_TwoAtOnce_SendsOnce()
    {
        // Arrange
        var provider = new CountingProvider { Delay = TimeSpan.FromMilliseconds(50) };
        var context = new DisbursementsTestContext(provider);
        var disbursement = (await context.Service.CreateAsync(Manual("k-4", 10m, method: provider.Method), TestContext.Current.CancellationToken)).Disbursement;
        var (batch, _) = await context.Service.CreateBatchAsync(provider.Name, [disbursement.ItemId], Arrival, TestContext.Current.CancellationToken);

        // Act
        var results = await Task.WhenAll(
            Task.Run(() => context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken)),
            Task.Run(() => context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken)));

        // Assert
        Assert.Equal(1, provider.Calls);
        Assert.Single(results, errors => errors.Count == 0);
        Assert.Single(results, errors => errors.Count == 1);
        Assert.Equal(DisbursementBatchStatus.Settled, batch.Status);
    }

    [Fact]
    public async Task ReleaseBatchAsync_AProviderThatThrowsAfterSending_LeavesItReleasing_AndItIsNotSentAgainUntilReopened()
    {
        // Arrange
        var provider = new CountingProvider { Throw = true };
        var context = new DisbursementsTestContext(provider);
        var disbursement = (await context.Service.CreateAsync(Manual("k-5", 10m, method: provider.Method), TestContext.Current.CancellationToken)).Disbursement;
        var (batch, _) = await context.Service.CreateBatchAsync(provider.Name, [disbursement.ItemId], Arrival, TestContext.Current.CancellationToken);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken));
        var stored = context.BatchStore.Entries.Single();
        var again = await context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken);
        provider.Throw = false;
        var reopened = await context.Service.ReopenReleasingBatchAsync(batch, "Checked with the provider", TestContext.Current.CancellationToken);
        var released = await context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DisbursementBatchStatus.Settled, stored.Status);
        Assert.Contains("reopen", Assert.Single(again), StringComparison.OrdinalIgnoreCase);
        Assert.True(reopened);
        Assert.Empty(released);
        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task ApplyReturns_ToADisbursementThePersonMayNotActOn_IsReportedAsNotFound()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var payee = await context.PayeeAsync();
        var groupA = await context.AchAsync(origin, payee, 10m, "statement-a", "groupA");
        var (batch, _) = await context.Service.CreateBatchAsync("Nacha", [groupA.ItemId], Arrival, TestContext.Current.CancellationToken);
        await context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken);
        var lee = new SourceAccessFilter(["PayStatement"], []);

        // Act
        var result = await context.Service.ApplyReturnsAsync([new DisbursementReturn(groupA.TraceNumber, "R02", null)], lee, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, result.Returned);
        Assert.Single(result.Problems);
        Assert.Equal(DisbursementStatus.Submitted, groupA.Status);
    }

    [Fact]
    public async Task ApplyReturns_StoresTheCode_AndTheTextIsLocalizedWhereItShows()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var payee = await context.PayeeAsync();
        var ach = await context.AchAsync(origin, payee, 10m);
        var (batch, _) = await context.Service.CreateBatchAsync("Nacha", [ach.ItemId], Arrival, TestContext.Current.CancellationToken);
        await context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken);
        var (returns, _, _) = NachaReturnFileReader.Read(NachaFileTests.ReturnFile(ach.TraceNumber, "R02"));

        // Act
        await context.Service.ApplyReturnsAsync(returns, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DisbursementStatus.Returned, ach.Status);
        Assert.Equal("R02", ach.FailureCode);
        Assert.Null(ach.FailureReason);
        Assert.Equal("Account closed", NachaReasonCodes.Describe("R02", RecipeRoundTrip.Localizer<DisbursementReviewFixesTests>())?.Value);
    }

    [Fact]
    public async Task ApplyChangeNotices_WithoutManageBankAccounts_IsReported_AndWithIt_TheOwnersObserversHear()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var payee = await context.PayeeAsync();
        var ach = await context.AchAsync(origin, payee, 10m);
        var (batch, _) = await context.Service.CreateBatchAsync("Nacha", [ach.ItemId], Arrival, TestContext.Current.CancellationToken);
        await context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken);
        var notice = new DisbursementChangeNotice(ach.TraceNumber, "C01", "987654321");
        context.Banking.Observer.Changes.Clear();

        // Act
        var refused = await context.Service.ApplyChangeNoticesAsync([notice], SourceAccessFilter.Unrestricted, false, TestContext.Current.CancellationToken);
        var heardBefore = context.Banking.Observer.Changes.Count;
        var applied = await context.Service.ApplyChangeNoticesAsync([notice], SourceAccessFilter.Unrestricted, true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, refused.Corrected);
        Assert.Contains("manage bank accounts", Assert.Single(refused.Problems), StringComparison.Ordinal);
        Assert.Equal(0, heardBefore);
        Assert.Equal(1, applied.Corrected);
        Assert.NotEqual(BankAccountStatus.Verified, payee.Status);
        Assert.Contains(context.Banking.Observer.Changes, change => change.Account.ItemId == payee.ItemId);
    }

    [Fact]
    public async Task SettleReleasedBatches_CountsFromTheArrivalDay_WhenItIsAfterTheRelease()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var check = (await context.Service.CreateAsync(new DisbursementRequest { PayeeName = "Pat", Amount = 10m, Method = "Check", SourceBankAccountId = origin.ItemId, IdempotencyKey = "k-6-check" }, TestContext.Current.CancellationToken)).Disbursement;
        var arrival = DateOnly.FromDateTime(context.Banking.Now).AddDays(10);
        var (batch, _) = await context.Service.CreateBatchAsync("Check", [check.ItemId], arrival, TestContext.Current.CancellationToken);
        await context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken);

        // Act
        context.Banking.Now = context.Banking.Now.AddDays(5);
        var early = await context.Service.SettleReleasedBatchesAsync(3, TestContext.Current.CancellationToken);
        context.Banking.Now = context.Banking.Now.AddDays(10);
        var later = await context.Service.SettleReleasedBatchesAsync(3, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, early);
        Assert.Equal(1, later);
    }

    [Fact]
    public async Task CancelBatchAsync_ReportsTheMoveBackToPending_AsUnbatched()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var payee = await context.PayeeAsync();
        var ach = await context.AchAsync(origin, payee, 10m);
        var (batch, _) = await context.Service.CreateBatchAsync("Nacha", [ach.ItemId], Arrival, TestContext.Current.CancellationToken);

        // Act
        await context.Service.CancelBatchAsync(batch, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DisbursementChangeKind.Unbatched, context.Observer.Changes.Last().Kind);
        Assert.Equal(DisbursementStatus.Pending, ach.Status);
    }

    [Fact]
    public async Task CreateBatchAsync_WithAPastArrivalDay_IsRefused_AndDisbursementsOfTwoSourcesAreBatchedApart()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var payee = await context.PayeeAsync();
        var groupA = await context.AchAsync(origin, payee, 10m, "statement-a", "groupA");
        var groupB = await context.AchAsync(origin, payee, 20m, "statement-b", "groupB");

        // Act
        var (past, pastProblems) = await context.Service.CreateBatchAsync("Nacha", [groupA.ItemId], DateOnly.FromDateTime(context.Banking.Now).AddDays(-1), TestContext.Current.CancellationToken);
        var (batch, problems) = await context.Service.CreateBatchAsync("Nacha", [groupA.ItemId, groupB.ItemId], Arrival, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(past);
        Assert.Contains("past", Assert.Single(pastProblems), StringComparison.Ordinal);
        Assert.Equal([groupA.ItemId], batch.DisbursementIds);
        Assert.Equal(("PayStatement", "groupA"), (batch.SourceKind, batch.AccessScope));
        Assert.Single(problems);
        Assert.Equal(DisbursementStatus.Pending, groupB.Status);
    }

    [Fact]
    public async Task BankFile_UsesTheBatchsEntryDescription_AndRefusesWithoutOne()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var payee = await context.PayeeAsync();
        var first = await context.AchAsync(origin, payee, 10m, "statement-1");
        var second = await context.AchAsync(origin, payee, 20m, "statement-2");
        var (payroll, _) = await context.Service.CreateBatchAsync("Nacha", [first.ItemId], Arrival, "payroll", TestContext.Current.CancellationToken);

        // Act
        var released = await context.Service.ReleaseBatchAsync(payroll, TestContext.Current.CancellationToken);
        var text = System.Text.Encoding.ASCII.GetString(context.Files.Files[payroll.ItemId]);
        var settings = origin.TryGet<NachaOriginSettings>(out var stored) ? stored : null;
        settings.CompanyEntryDescription = null;
        origin.Put(settings);
        await context.Banking.Manager.UpdateAsync(origin, cancellationToken: TestContext.Current.CancellationToken);
        var (plain, _) = await context.Service.CreateBatchAsync("Nacha", [second.ItemId], Arrival, TestContext.Current.CancellationToken);
        var refused = await context.Service.ReleaseBatchAsync(plain, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(released);
        Assert.Equal("PAYROLL   ", text.Split("\r\n")[1].Substring(53, 10));
        Assert.Contains("entry description", Assert.Single(refused), StringComparison.Ordinal);
        Assert.Equal(DisbursementBatchStatus.Open, plain.Status);
    }

    [Fact]
    public void BankFile_AnAccountNumberOver17Characters_IsRefused_NotCut()
    {
        // Arrange
        var entry = NachaFileTests.Person("long", Banking.BankingTestContext.ValidRoutingNumber, 100);
        entry.AccountNumber = "123456789012345678";
        var request = NachaFileTests.Request(entry);

        // Act + Assert
        Assert.Throws<ArgumentException>(() => NachaFileWriter.Write(request));
    }

    [Fact]
    public async Task BankFileCard_NotOnTheForm_KeepsTheStoredSettings()
    {
        // Arrange: an account that sends bank files, saved from a form drawn while it had an owner (no card on it).
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var driver = new CrestApps.OrchardCore.Disbursements.Drivers.NachaOriginSettingsDisplayDriver(RecipeRoundTrip.Localizer<CrestApps.OrchardCore.Disbursements.Drivers.NachaOriginSettingsDisplayDriver>());

        async Task<NachaOriginSettings> SaveAsync(CrestApps.OrchardCore.Disbursements.ViewModels.NachaOriginSettingsViewModel posted)
        {
            var updater = new CrestApps.OrchardCore.Tests.Taxation.Fakes.FakeUpdateModel(posted);

            await driver.UpdateAsync(origin, new global::OrchardCore.DisplayManagement.Handlers.UpdateEditorContext(null, string.Empty, false, string.Empty, null, null, updater));

            return origin.TryGet<NachaOriginSettings>(out var stored) ? stored : null;
        }

        // Act
        var kept = await SaveAsync(new CrestApps.OrchardCore.Disbursements.ViewModels.NachaOriginSettingsViewModel());
        var turnedOff = await SaveAsync(new CrestApps.OrchardCore.Disbursements.ViewModels.NachaOriginSettingsViewModel { Shown = true });

        // Assert
        Assert.Equal((true, "CONTOSO LTD"), (kept.Enabled, kept.CompanyName));
        Assert.False(turnedOff.Enabled);
    }

    private static DateOnly Arrival
        => new(2026, 11, 16);

    private static DisbursementRequest Manual(string key, decimal amount, string payee = "Kim", string method = "Manual")
        => new()
        {
            Payee = new PartyRef("Staff", payee),
            PayeeName = payee,
            Amount = amount,
            Currency = "USD",
            Method = method,
            IdempotencyKey = key,
        };

    private sealed class CountingProvider : IDisbursementProvider
    {
        private int _calls;

        public string Name
            => "Counting";

        public LocalizedString DisplayName
            => new("Counting", "Counting");

        public string Method
            => "Counting";

        public int Calls
            => _calls;

        public TimeSpan Delay { get; set; }

        public bool Throw { get; set; }

        public async Task<DisbursementSubmitResult> SubmitAsync(DisbursementBatch batch, IReadOnlyList<Disbursement> disbursements, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);

            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            if (Throw)
            {
                throw new InvalidOperationException("The provider failed after it may have sent.");
            }

            var result = new DisbursementSubmitResult { Succeeded = true };

            foreach (var disbursement in disbursements)
            {
                result.Statuses[disbursement.ItemId] = DisbursementStatus.Settled;
                result.TraceNumbers[disbursement.ItemId] = "t-" + disbursement.ItemId;
            }

            return result;
        }
    }
}
