using System.Text;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Checks.Models;
using CrestApps.OrchardCore.Disbursements.Core.Nacha;
using CrestApps.OrchardCore.Disbursements.Models;

namespace CrestApps.OrchardCore.Tests.Disbursements;

/// <summary>
/// The rules of disbursements: recorded once by key, checked against the bank accounts, batched, released through
/// the bank file, check and manual providers, settled, returned and corrected, and the prenote that verifies an
/// account.
/// </summary>
public sealed class DisbursementServiceTests
{
    [Fact]
    public async Task CreateAsync_AskingTwiceWithTheSameKey_GivesTheSameDisbursement()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var payee = await context.PayeeAsync();

        // Act
        var first = await context.AchAsync(origin, payee);
        var again = await context.Service.CreateAsync(new DisbursementRequest
        {
            Payee = payee.Owner,
            PayeeName = "Pat Doe",
            Amount = 1250.50m,
            Method = "Ach",
            SourceBankAccountId = origin.ItemId,
            DestinationBankAccountId = payee.ItemId,
            SourceKind = "PayStatement",
            SourceId = "statement-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(again.AlreadyExisted);
        Assert.Equal(first.ItemId, again.Disbursement.ItemId);
        Assert.Single(context.Store.Entries);
    }

    [Fact]
    public async Task CreateAsync_ToAnUnverifiedAccount_IsRefused_ButATestEntryGoesToIt()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var payee = await context.PayeeAsync(verified: false);

        // Act
        var payment = await context.Service.CreateAsync(new DisbursementRequest { PayeeName = "Pat", Amount = 10, Method = "Ach", SourceBankAccountId = origin.ItemId, DestinationBankAccountId = payee.ItemId, IdempotencyKey = "k1" }, TestContext.Current.CancellationToken);
        var prenote = await context.Service.CreateAsync(new DisbursementRequest { PayeeName = "Pat", Amount = 0, Method = "Ach", SourceBankAccountId = origin.ItemId, DestinationBankAccountId = payee.ItemId, IdempotencyKey = "k2", IsPrenote = true }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(payment.Succeeded);
        Assert.True(prenote.Succeeded, string.Join(" ", prenote.Errors));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    [InlineData(5, true)]
    public async Task CreateAsync_AnAmountThatIsNotMoreThanZero_IsRefused(decimal amount, bool succeeds)
    {
        // Arrange
        var context = new DisbursementsTestContext();

        // Act
        var result = await context.Service.CreateAsync(new DisbursementRequest { PayeeName = "Pat", Amount = amount, Currency = "USD", Method = "Manual", IdempotencyKey = "k" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(succeeds, result.Succeeded);
    }

    [Fact]
    public async Task CreateAsync_WithoutAKeyOrASource_IsRefused()
    {
        // Act
        var result = await new DisbursementsTestContext().Service.CreateAsync(new DisbursementRequest { PayeeName = "Pat", Amount = 5, Currency = "USD", Method = "Manual" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CreateBatchAsync_LeavesOutWhatTheProviderDoesNotSend()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var payee = await context.PayeeAsync();
        var ach = await context.AchAsync(origin, payee);
        var manual = (await context.Service.CreateAsync(new DisbursementRequest { PayeeName = "Lee", Amount = 5, Currency = "USD", Method = "Manual", IdempotencyKey = "m1" }, TestContext.Current.CancellationToken)).Disbursement;

        // Act
        var (batch, problems) = await context.Service.CreateBatchAsync("Nacha", [ach.ItemId, manual.ItemId], new DateOnly(2026, 11, 16), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([ach.ItemId], batch.DisbursementIds);
        Assert.Single(problems);
        Assert.Equal(DisbursementStatus.Batched, ach.Status);
        Assert.Equal(DisbursementStatus.Pending, manual.Status);
        Assert.Equal(1250.50m, batch.TotalAmount);
    }

    [Fact]
    public async Task ReleaseBatchAsync_ByBankFile_StoresAFileWithEachTraceNumber_ReadingEachAccountNumberOnce()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var payee = await context.PayeeAsync("123456789");
        var first = await context.AchAsync(origin, payee, 100m, "statement-1");
        var second = await context.AchAsync(origin, payee, 200m, "statement-2");
        var (batch, _) = await context.Service.CreateBatchAsync("Nacha", [first.ItemId, second.ItemId], new DateOnly(2026, 11, 16), TestContext.Current.CancellationToken);
        context.Banking.Observer.Changes.Clear();

        // Act
        var errors = await context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(errors);
        Assert.Equal(DisbursementBatchStatus.Released, batch.Status);
        Assert.True(batch.HasFile);
        var text = Encoding.ASCII.GetString(context.Files.Files[batch.ItemId]);
        Assert.Contains("123456789", text, StringComparison.Ordinal);
        Assert.Equal(DisbursementStatus.Submitted, first.Status);
        Assert.Equal("021000020000001", first.TraceNumber);
        Assert.Equal("021000020000002", second.TraceNumber);
        Assert.Single(context.Banking.Observer.Changes, change => change.Has(BankAccountChangeKind.NumberRevealed));
    }

    [Fact]
    public async Task ReleaseBatchAsync_TwoFilesFromOneBank_NeverReuseATraceNumber()
    {
        // Arrange: a return names its entry by trace number alone, so a second file must not start at 1 again.
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var payee = await context.PayeeAsync();
        var first = await context.AchAsync(origin, payee, 100m, "statement-1");
        var second = await context.AchAsync(origin, payee, 200m, "statement-2");
        var (batch1, _) = await context.Service.CreateBatchAsync("Nacha", [first.ItemId], new DateOnly(2026, 11, 16), TestContext.Current.CancellationToken);
        await context.Service.ReleaseBatchAsync(batch1, TestContext.Current.CancellationToken);
        var (batch2, _) = await context.Service.CreateBatchAsync("Nacha", [second.ItemId], new DateOnly(2026, 11, 17), TestContext.Current.CancellationToken);

        // Act
        await context.Service.ReleaseBatchAsync(batch2, TestContext.Current.CancellationToken);
        await context.Service.ApplyReturnsAsync([new DisbursementReturn(second.TraceNumber, "R03", "No account")], TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(first.TraceNumber, second.TraceNumber);
        Assert.Equal(DisbursementStatus.Returned, second.Status);
        Assert.Equal(DisbursementStatus.Submitted, first.Status);
    }

    [Fact]
    public async Task ReleaseBatchAsync_FromAnAccountNotSetUpForBankFiles_IsRefused()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var origin = await context.Banking.CreateAsync("900000001", name: "Operating");
        var payee = await context.PayeeAsync();
        var ach = await context.AchAsync(origin, payee);
        var (batch, _) = await context.Service.CreateBatchAsync("Nacha", [ach.ItemId], new DateOnly(2026, 11, 16), TestContext.Current.CancellationToken);

        // Act
        var errors = await context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(errors, error => error.Contains("Operating", StringComparison.Ordinal));
        Assert.Equal(DisbursementBatchStatus.Open, batch.Status);
        Assert.Equal(DisbursementStatus.Batched, ach.Status);
    }

    [Fact]
    public async Task ReleaseBatchAsync_PaidOutsideTheSystem_SettlesAtOnce()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var manual = (await context.Service.CreateAsync(new DisbursementRequest { PayeeName = "Lee", Amount = 5, Currency = "USD", Method = "Manual", IdempotencyKey = "m1" }, TestContext.Current.CancellationToken)).Disbursement;
        var (batch, _) = await context.Service.CreateBatchAsync("Manual", [manual.ItemId], new DateOnly(2026, 11, 16), TestContext.Current.CancellationToken);

        // Act
        await context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DisbursementStatus.Settled, manual.Status);
        Assert.Equal(DisbursementBatchStatus.Settled, batch.Status);
        Assert.False(batch.HasFile);
    }

    [Fact]
    public async Task CancelBatchAsync_PutsItsDisbursementsBackToPending()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var ach = await context.AchAsync(origin, await context.PayeeAsync());
        var (batch, _) = await context.Service.CreateBatchAsync("Nacha", [ach.ItemId], new DateOnly(2026, 11, 16), TestContext.Current.CancellationToken);

        // Act
        var cancelled = await context.Service.CancelBatchAsync(batch, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(cancelled);
        Assert.Equal(DisbursementStatus.Pending, ach.Status);
        Assert.Null(ach.BatchId);
        Assert.Equal(DisbursementBatchStatus.Canceled, batch.Status);
    }

    [Fact]
    public async Task SettleReleasedBatchesAsync_SettlesOnlyBatchesOlderThanTheAge()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var ach = await context.AchAsync(origin, await context.PayeeAsync());
        var (batch, _) = await context.Service.CreateBatchAsync("Nacha", [ach.ItemId], new DateOnly(2026, 11, 16), TestContext.Current.CancellationToken);
        await context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken);

        // Act: three days are counted from the arrival day (the 16th), the later of it and the release.
        var tooSoon = await context.Service.SettleReleasedBatchesAsync(3, TestContext.Current.CancellationToken);
        context.Banking.Now = context.Banking.Now.AddDays(4);
        var stillTooSoon = await context.Service.SettleReleasedBatchesAsync(3, TestContext.Current.CancellationToken);
        context.Banking.Now = context.Banking.Now.AddDays(3);
        var settled = await context.Service.SettleReleasedBatchesAsync(3, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, tooSoon);
        Assert.Equal(0, stillTooSoon);
        Assert.Equal(1, settled);
        Assert.Equal(DisbursementStatus.Settled, ach.Status);
    }

    [Fact]
    public async Task ApplyReturnsAsync_MarksTheDisbursementReturned_WithTheCode()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var ach = await context.AchAsync(origin, await context.PayeeAsync());
        var (batch, _) = await context.Service.CreateBatchAsync("Nacha", [ach.ItemId], new DateOnly(2026, 11, 16), TestContext.Current.CancellationToken);
        await context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken);

        // Act
        var result = await context.Service.ApplyReturnsAsync([new DisbursementReturn(ach.TraceNumber, "R02", "Account closed"), new DisbursementReturn("999", "R01", "x")], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, result.Returned);
        Assert.Single(result.Problems);
        Assert.Equal(DisbursementStatus.Returned, ach.Status);
        Assert.Equal("R02", ach.FailureCode);
        Assert.Equal(DisbursementChangeKind.Returned, context.Observer.Changes[^1].Kind);
    }

    [Fact]
    public async Task ApplyChangeNoticesAsync_CorrectsThePayeesAccount_WhichIsUnverifiedAgain()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var payee = await context.PayeeAsync("123456789");
        var ach = await context.AchAsync(origin, payee);
        var (batch, _) = await context.Service.CreateBatchAsync("Nacha", [ach.ItemId], new DateOnly(2026, 11, 16), TestContext.Current.CancellationToken);
        await context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken);

        // Act
        var result = await context.Service.ApplyChangeNoticesAsync([new DisbursementChangeNotice(ach.TraceNumber, "C01", "987654321")], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, result.Corrected);
        var account = await context.Banking.Manager.FindByIdAsync(payee.ItemId, TestContext.Current.CancellationToken);
        Assert.Equal("987654321", await context.Banking.Service.RevealAccountNumberAsync(account, "test", TestContext.Current.CancellationToken));
        Assert.Equal(BankAccountStatus.Unverified, account.Status);
    }

    [Fact]
    public async Task Prenote_SettledWithoutAReturn_VerifiesTheAccount()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        await context.OriginAsync();
        var payee = await context.PayeeAsync(verified: false);

        // Act
        var started = await context.PrenoteVerifier().StartAsync(payee, TestContext.Current.CancellationToken);
        Assert.True(started.Succeeded, started.Message);
        Assert.True(context.Banking.Service.TrySetStatus(payee, started.Status, null, NachaPrenoteVerifier.VerifierName));
        await context.Banking.Manager.UpdateAsync(payee, cancellationToken: TestContext.Current.CancellationToken);
        var prenote = Assert.Single(context.Store.Entries);
        var (batch, _) = await context.Service.CreateBatchAsync("Nacha", [prenote.ItemId], new DateOnly(2026, 11, 16), TestContext.Current.CancellationToken);
        await context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken);
        await context.Service.SettleBatchAsync(batch, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(prenote.IsPrenote);
        Assert.Equal(0, prenote.Amount);
        Assert.Equal(BankAccountStatus.Verified, (await context.Banking.Manager.FindByIdAsync(payee.ItemId, TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task Prenote_Returned_FailsTheAccount()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        await context.OriginAsync();
        var payee = await context.PayeeAsync(verified: false);
        await context.PrenoteVerifier().StartAsync(payee, TestContext.Current.CancellationToken);
        Assert.True(context.Banking.Service.TrySetStatus(payee, BankAccountStatus.VerificationSent));
        await context.Banking.Manager.UpdateAsync(payee, cancellationToken: TestContext.Current.CancellationToken);
        var prenote = Assert.Single(context.Store.Entries);
        var (batch, _) = await context.Service.CreateBatchAsync("Nacha", [prenote.ItemId], new DateOnly(2026, 11, 16), TestContext.Current.CancellationToken);
        await context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken);

        // Act
        await context.Service.ApplyReturnsAsync([new DisbursementReturn(prenote.TraceNumber, "R03", "No account")], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(BankAccountStatus.Failed, (await context.Banking.Manager.FindByIdAsync(payee.ItemId, TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task Prenote_WithoutAnAccountSetUpForBankFiles_IsRefused()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var payee = await context.PayeeAsync(verified: false);

        // Act
        var started = await context.PrenoteVerifier().StartAsync(payee, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(started.Succeeded);
    }

    [Fact]
    public async Task ByCheck_ReleasingIssuesChecks_AndAClearedCheckSettlesTheDisbursement()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var check = (await context.Service.CreateAsync(new DisbursementRequest { PayeeName = "Lee Doe", Amount = 75, Method = "Check", SourceBankAccountId = origin.ItemId, SourceKind = "PayStatement", SourceId = "s9", AccessScope = "employer-1" }, TestContext.Current.CancellationToken)).Disbursement;
        var (batch, _) = await context.Service.CreateBatchAsync("Check", [check.ItemId], new DateOnly(2026, 11, 16), TestContext.Current.CancellationToken);

        // Act
        await context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken);
        var issued = Assert.Single(context.Checks.Store.Entries);
        await context.Checks.Service.MarkPrintedAsync([issued], "run1", TestContext.Current.CancellationToken);
        await context.Checks.Service.MarkClearedAsync(issued, new DateOnly(2026, 11, 20), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(issued.ItemId, check.CheckId);
        Assert.Equal("PayStatement", issued.SourceKind);
        Assert.Equal("employer-1", issued.AccessScope);
        Assert.Equal(new DateOnly(2026, 11, 16), issued.CheckDate);
        Assert.Equal(DisbursementStatus.Settled, check.Status);
    }

    [Fact]
    public async Task ByCheck_AVoidedCheck_CancelsTheDisbursement_AndAReissueFollowsTheNewCheck()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var first = (await context.Service.CreateAsync(new DisbursementRequest { PayeeName = "Lee", Amount = 75, Method = "Check", SourceBankAccountId = origin.ItemId, IdempotencyKey = "c1" }, TestContext.Current.CancellationToken)).Disbursement;
        var second = (await context.Service.CreateAsync(new DisbursementRequest { PayeeName = "Kim", Amount = 80, Method = "Check", SourceBankAccountId = origin.ItemId, IdempotencyKey = "c2" }, TestContext.Current.CancellationToken)).Disbursement;
        var (batch, _) = await context.Service.CreateBatchAsync("Check", [first.ItemId, second.ItemId], new DateOnly(2026, 11, 16), TestContext.Current.CancellationToken);
        await context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken);
        var firstCheck = context.Checks.Store.Entries.Single(entry => entry.ItemId == first.CheckId);
        var secondCheck = context.Checks.Store.Entries.Single(entry => entry.ItemId == second.CheckId);

        // Act
        await context.Checks.Service.VoidAsync(firstCheck, "Spoiled", TestContext.Current.CancellationToken);
        var reissue = await context.Checks.Service.ReissueAsync(secondCheck, "Lost", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(DisbursementStatus.Canceled, first.Status);
        Assert.Equal(reissue.Check.ItemId, (await context.Manager.FindByIdAsync(second.ItemId, TestContext.Current.CancellationToken)).CheckId);
        Assert.Equal(CheckStatus.Reissued, secondCheck.Status);
    }

    [Fact]
    public async Task DeletingABankAccountMoneyWasSentTo_IsRefused()
    {
        // Arrange
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        var payee = await context.PayeeAsync();
        await context.AchAsync(origin, payee);

        // Act
        var usages = await context.Banking.Service.GetUsagesAsync(payee, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Money was sent from or to it.", usages);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await context.Banking.Manager.DeleteAsync(payee, TestContext.Current.CancellationToken));
    }
}
